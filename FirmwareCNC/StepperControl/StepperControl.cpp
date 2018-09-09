#include "StepperControl.h"


bool INSTRUCTION_ENDS[SCHEDULE_BUFFER_LEN + 1] = { 0 };
uint16_t SCHEDULE_BUFFER[SCHEDULE_BUFFER_LEN + 1] = { 0 };
byte SCHEDULE_ACTIVATIONS[SCHEDULE_BUFFER_LEN + 1] = { 0 };
byte CUMULATIVE_SCHEDULE_ACTIVATION = 0;



volatile byte SCHEDULE_START = 0;
volatile byte SCHEDULE_END = 0;

volatile byte ACTIVATION_MASK = 0;
volatile bool SCHEDULER_STOP_EVENT_FLAG = false;
volatile bool SCHEDULER_START_EVENT_FLAG = false;

volatile int32_t SLOT0_STEPS = 0;
volatile int32_t SLOT1_STEPS = 0;
volatile int32_t SLOT2_STEPS = 0;
volatile int32_t SLOT3_STEPS = 0;

ISR(TIMER1_OVF_vect) {
	//pins go LOW here (pulse start)
	byte activation = SCHEDULE_ACTIVATIONS[SCHEDULE_END] | ACTIVATION_MASK;

	//THE TIMER RESET IS TUNED HERE (!!!NO CHANGES BEFORE THIS!!!)
	TCNT1 = SCHEDULE_BUFFER[SCHEDULE_END];

	PORTB = B_SLOTS_MASK & activation;
	PORTD = D_SLOTS_MASK & activation;

	bool isInstructionEnd = INSTRUCTION_ENDS[SCHEDULE_END];
	if (SCHEDULE_START == SCHEDULE_END) {
		//we are at schedule end
		TIMSK1 = 0;
		SCHEDULER_STOP_EVENT_FLAG = true;
	}
	else {
		++SCHEDULE_END;
	}
	//---HERE WE HAVE TO spent 3us at least (because of the minimal pulse width)
	byte nactivation = ~activation;
	byte step0 = nactivation & (SLOT0_CLK_MASK | SLOT0_DIR_MASK);
	int8_t step0p = (step0 == (SLOT0_CLK_MASK | SLOT0_DIR_MASK));
	int8_t step0n = (step0 == SLOT0_CLK_MASK);

	byte step1 = nactivation & (SLOT1_CLK_MASK | SLOT1_DIR_MASK);
	int8_t step1p = (step1 == (SLOT1_CLK_MASK | SLOT1_DIR_MASK));
	int8_t step1n = (step1 == SLOT1_CLK_MASK);

	byte step2 = nactivation & (SLOT2_CLK_MASK | SLOT2_DIR_MASK);
	int8_t step2p = (step2 == (SLOT2_CLK_MASK | SLOT2_DIR_MASK));
	int8_t step2n = (step2 == SLOT2_CLK_MASK);

	byte step3 = nactivation & (SLOT3_CLK_MASK | SLOT3_DIR_MASK);
	int8_t step3p = (step3 == (SLOT3_CLK_MASK | SLOT3_DIR_MASK));
	int8_t step3n = (step3 == SLOT3_CLK_MASK);

	SLOT0_STEPS += (int32_t)(step0p - step0n);
	SLOT1_STEPS += (int32_t)(step1p - step1n);
	SLOT2_STEPS += (int32_t)(step2p - step2n);
	SLOT3_STEPS += (int32_t)(step3p - step3n);	

	/*	Serial.print('|');
	Serial.print(step2p);
	Serial.print(' ');
	Serial.print(step2n);
	Serial.print(' ');
	Serial.println(SLOT2_STEPS);*/
	//------------------------------------

	//pins go HIGH here (pulse end)
	if (isInstructionEnd)
		Serial.write('F');
	PORTB |= B_SLOTS_MASK & ACTIVATIONS_CLOCK_MASK;
	PORTD |= D_SLOTS_MASK & ACTIVATIONS_CLOCK_MASK;

}

bool Steppers::startScheduler() {
	if (TIMSK1 != 0) {
		//scheduler is already enabled
		//we are free to do other things
		return true;
	}

	if (SCHEDULE_START == SCHEDULE_END)
		//schedule is empty - no point in schedule enabling
		return false;


	Serial.print('S'); //enabling scheduler

	SCHEDULER_START_EVENT_FLAG = true;
	TCNT1 = SCHEDULE_BUFFER[SCHEDULE_END++];
	TIMSK1 = (1 << TOIE1); //enable scheduler

	return false;
}

void Steppers::setActivationMask(byte mask) {
	ACTIVATION_MASK = mask;
}

bool Steppers::isSchedulerRunning()
{
	return TIMSK1 > 0;
}

void Steppers::initialize()
{
	noInterrupts(); // disable all interrupts
	TCCR1A = 0;
	TCCR1B = 0;
	TIMSK1 = 0;

	TCCR1B |= 1 << CS11; // 8 prescaler
	interrupts(); // enable all interrupts
}


AccelerationPlan::AccelerationPlan(byte clkPin, byte dirPin)
	: Plan(clkPin, dirPin), _currentDeltaT(0), _current4N(0), _currentDeltaTBuffer2(0), _isDeceleration(false)
{
}

void AccelerationPlan::loadFrom(byte * data)
{
	int16_t stepCount = READ_INT16(data, 0);
	int32_t initialDeltaT = READ_INT32(data, 2);
	int32_t n = READ_INT32(data, 2 + 4);
	int16_t baseDelta = READ_INT16(data, 2 + 4 + 4);
	int16_t baseRemainder = READ_INT16(data, 2 + 4 + 4 + 2);

	this->stepCount = abs(stepCount);
	this->remainingSteps = this->stepCount;
	this->isActive = this->remainingSteps > 0;
	this->stepMask = stepCount < 0 ? this->dirMask : 0;
	this->nextActivationTime = 0;
	this->isActivationBoundary = !this->isActive;

	this->_isDeceleration = n < 0;
	this->_baseDeltaT = baseDelta;
	this->_baseRemainder = abs(baseRemainder);
	this->_baseRemainderBuffer = this->_baseRemainder / 2;
	this->_currentDeltaT = initialDeltaT;
	this->_current4N = ((uint32_t)4) * abs(n);
	this->_currentDeltaTBuffer2 = 0;
}

void AccelerationPlan::initForHoming()
{
	//TODO refactore homing settings somewhere
	int16_t stepCount = -150;
	this->stepCount = abs(stepCount);
	this->remainingSteps = this->stepCount;
	this->isActive = this->remainingSteps > 0;
	this->stepMask = stepCount < 0 ? this->dirMask : 0;
	this->nextActivationTime = 0;
	this->isActivationBoundary = !this->isActive;

	int n = 6;
	this->_isDeceleration = n < 0;
	this->_baseDeltaT = 0;
	this->_baseRemainder = 0;
	this->_baseRemainderBuffer = this->_baseRemainder / 2;
	this->_currentDeltaT = 2000;
	this->_current4N = ((uint32_t)4) * abs(n);
	this->_currentDeltaTBuffer2 = 0;
}

void AccelerationPlan::createNextActivation()
{
	if (this->remainingSteps == 0) {
		this->isActive = false;
		return;
	}

	--this->remainingSteps;
	this->nextActivationTime = this->_currentDeltaT + this->_baseDeltaT;

	if (this->_baseRemainder > 0) {
		this->_baseRemainderBuffer += this->_baseRemainder;
		if (this->_baseRemainderBuffer > this->stepCount) {
			this->_baseRemainderBuffer -= this->stepCount;
			this->nextActivationTime += 1;
		}
	}

	if (this->_current4N == 0) {
		//compensate for error at c0
		this->_currentDeltaT = this->_currentDeltaT * 676 / 1000;
	}

	int32_t nextDeltaT = this->_currentDeltaT;
	int32_t nextDeltaTChange = 0;
	this->_currentDeltaTBuffer2 += nextDeltaT * 2;

	if (this->_isDeceleration) {
		this->_current4N -= 4;
	}
	else {
		this->_current4N += 4;
	}

	if (nextDeltaT > 5000) {
		//TODO find optimal boundary - we don't want to do zillion subtractions here
		nextDeltaTChange = this->_currentDeltaTBuffer2 / (this->_current4N + 1);
		this->_currentDeltaTBuffer2 = this->_currentDeltaTBuffer2 % (this->_current4N + 1);
	}
	else {
		//for small numbers we will do better with subtraction
		while (this->_currentDeltaTBuffer2 >= this->_current4N + 1) {
			this->_currentDeltaTBuffer2 -= this->_current4N + 1;
			nextDeltaTChange += 1;
		}
	}
	nextDeltaT = this->_isDeceleration ? nextDeltaT + nextDeltaTChange : nextDeltaT - nextDeltaTChange;
	this->_currentDeltaT = nextDeltaT;

	//Serial.print("|d:");
	//Serial.println(nextDeltaT);


}

void ConstantPlan::createNextActivation()
{
	if (this->remainingSteps == 0) {
		this->isActive = false;
		return;
	}

	--this->remainingSteps;

	int32_t currentDeltaT = this->_baseDeltaT;

	if (this->_periodNumerator > 0) {
		this->_periodAccumulator += this->_periodNumerator;
		if (this->_periodDenominator < this->_periodAccumulator) {
			this->_periodAccumulator -= this->_periodDenominator;
			currentDeltaT += 1;
		}
	}

	this->nextActivationTime = currentDeltaT;
}

ConstantPlan::ConstantPlan(byte clkPin, byte dirPin)
	:Plan(clkPin, dirPin),
	_baseDeltaT(0), _periodNumerator(0), _periodDenominator(0), _periodAccumulator(0)
{
}

void ConstantPlan::loadFrom(byte * data)
{
	int16_t stepCount = READ_INT16(data, 0);
	int32_t baseDeltaT = READ_INT32(data, 2);
	uint16_t periodNumerator = READ_UINT16(data, 2 + 4);
	uint16_t periodDenominator = READ_UINT16(data, 2 + 4 + 2);

	this->stepCount = abs(stepCount);
	this->remainingSteps = this->stepCount;
	this->stepMask = stepCount < 0 ? this->dirMask : 0;
	this->isActive = this->remainingSteps > 0;
	this->nextActivationTime = 0;
	this->isActivationBoundary = !this->isActive;

	this->_baseDeltaT = baseDeltaT;
	this->_periodNumerator = periodNumerator;
	this->_periodDenominator = periodDenominator;
	this->_periodAccumulator = 0;
	if (this->_periodNumerator > 0)
		this->_periodAccumulator = this->_periodDenominator / this->_periodNumerator;
}

void ConstantPlan::initForHoming()
{
	//TODO refactor homing settings somewhere
	int16_t stepCount = -200;
	this->stepCount = abs(stepCount);
	this->remainingSteps = this->stepCount;
	this->stepMask = stepCount < 0 ? this->dirMask : 0;
	this->isActive = this->remainingSteps > 0;
	this->nextActivationTime = 0;
	this->isActivationBoundary = !this->isActive;

	this->_baseDeltaT = 400;
	this->_periodNumerator = 0;
	this->_periodDenominator = 0;
	this->_periodAccumulator = 0;
}

Plan::Plan(byte clkMask, byte dirMask) :
	clkMask(clkMask), dirMask(dirMask),
	stepMask(0), stepCount(0), remainingSteps(0), isActive(false), isActivationBoundary(false), nextActivationTime(0)
{
}
