#include "StepperControl.h"

// how long (on 0.5us scale)
//	* before pulse the dir has to be specified 
//  * after pulse start pulse end has to be specified
// KEEPING BOTH VALUES SAME enables computation optimization
#define PORT_CHANGE_DELAY 5*2


// length of the schedule buffer (CANNOT be changed easily - it counts on byte overflows)
#define SCHEDULE_BUFFER_LEN 256

// buffer for step signal timing
uint16_t SCHEDULE_BUFFER[SCHEDULE_BUFFER_LEN + 1];
// bitwise activation mask for step signals (selecting active ports)
byte SCHEDULE_ACTIVATIONS[SCHEDULE_BUFFER_LEN + 1];
// cumulative activation with state up to lastly scheduled activation
byte CUMULATIVE_SCHEDULE_ACTIVATION = 0;

// pointer where new timing will be stored
volatile byte SCHEDULE_START = 0;
// pointer where scheduler is actually reading
volatile byte SCHEDULE_END = 0;

//TODO load this during initialization
volatile byte ACTIVATIONS_CLOCK_MASK = 1 + 4;

ISR(TIMER1_OVF_vect) {
	TCNT1 = SCHEDULE_BUFFER[SCHEDULE_END];

	//pins go LOW here (pulse start)
	PORTB = SCHEDULE_BUFFER[SCHEDULE_END++];
	if (SCHEDULE_START == SCHEDULE_END) {
		//we are at schedule end
		TIMSK1 = 0;
	}
	//pins go HIGH here (pulse end)
	PORTB |= ACTIVATIONS_CLOCK_MASK;
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

	TCNT1 = SCHEDULE_BUFFER[SCHEDULE_END++];
	TIMSK1 = (1 << TOIE1); //enable scheduler

	return false;
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


void Steppers::runPlanning(StepperGroup & group, Plan ** plans)
{
	Steppers::initPlanning(group, plans);

	bool hasPlan = true;
	while (hasPlan)
		hasPlan = Steppers::fillSchedule(group, plans);

	//free plan memory
	for (int i = 0; i < group.StepperCount; ++i)
		delete plans[i];

	delete plans;
}

inline bool Steppers::fillSchedule(StepperGroup & group, Plan ** plans)
{
	for (;;) {
		//find earliest plan
		uint16_t earliestActivationTime = 65535;
		for (int i = 0; i < group.StepperCount; ++i) {
			Plan* plan = plans[i];
			if (!plan->_isActive)
				continue;

			earliestActivationTime = min(earliestActivationTime, plan->_nextActivationTime);
		}

		CUMULATIVE_SCHEDULE_ACTIVATION |= ACTIVATIONS_CLOCK_MASK;

		//subtract deltaT from other plans		
		bool hasActivePlan = false;
		for (int i = 0; i < group.StepperCount; ++i) {
			Plan* plan = plans[i];
			if (!plan->_isActive)
				continue;

			hasActivePlan = true;
			plan->_nextActivationTime -= earliestActivationTime;

			if (plan->_nextActivationTime == 0) {
				//activation has to be scheduled
				if (!plan->_skipNextActivation)
					// make the appropriate pin LOW
					CUMULATIVE_SCHEDULE_ACTIVATION &= ~(group._clockBports[i]);

				if (plan->_nextStepDirection) {
					CUMULATIVE_SCHEDULE_ACTIVATION |= group._dirBports[i];
				}
				else {
					CUMULATIVE_SCHEDULE_ACTIVATION &= ~group._dirBports[i];
				}

				//compute next activation
				plan->_createNextActivation();
			}
		}

		if (!hasActivePlan)
			//there is not any active plan - finish
			break;

		//schedule
		while ((byte)(SCHEDULE_START + 1) == SCHEDULE_END) {
			//wait until schedule buffer has empty space
			Steppers::startScheduler();
		}

		SCHEDULE_BUFFER[SCHEDULE_START] = 65535 - earliestActivationTime;
		SCHEDULE_ACTIVATIONS[SCHEDULE_START + 1] = CUMULATIVE_SCHEDULE_ACTIVATION;
		//we can shift the start after activation is properly saved to array
		++SCHEDULE_START;

		/*/Serial.print("| t:");
		Serial.print(earliestActivationTime);
		Serial.print(", a:");
		Serial.println(CUMULATIVE_SCHEDULE_ACTIVATION);//*/

		if ((byte)(SCHEDULE_START + 1) == SCHEDULE_END)
			//we have free time
			return true;
	}
	Steppers::startScheduler();
	return false;
}



void Steppers::initPlanning(StepperGroup & group, Plan ** plans)
{
	for (int i = 0; i < group.StepperCount; ++i) {
		plans[i]->_createNextActivation();
	}
}

void Steppers::directScheduleFill(byte* activations, int16_t* timing, int count) {
	for (int i = 0; i < count; ++i) {
		byte activation = activations[i];
		int16_t time = timing[i];

		if ((byte)(SCHEDULE_START + 1) == SCHEDULE_END) {
			Serial.print('X');
		}

		SCHEDULE_BUFFER[SCHEDULE_START] = 65535 - time;
		SCHEDULE_ACTIVATIONS[SCHEDULE_START++] = activation;
	}
}

StepperGroup::StepperGroup(byte stepperCount, byte clockPins[], byte dirPins[])
	:StepperCount(stepperCount)
{
	this->_clockBports = new byte[stepperCount];
	this->_dirBports = new byte[stepperCount];
	for (int i = 0; i < stepperCount; ++i) {
		this->_clockBports[i] = 1 << (clockPins[i] - 8);
		this->_dirBports[i] = 1 << (dirPins[i] - 8);
	}
}

Plan::Plan(int32_t stepCount) :
	_nextStepDirection(stepCount > 0), _remainingSteps(abs(stepCount)),
	_isActive(true), _skipNextActivation(false),
	_nextActivationTime(0),
	_nextDeactivationTime(-1)
{
}

AccelerationPlan::AccelerationPlan(int16_t stepCount, uint16_t initialDeltaT, int16_t n)
	: Plan(stepCount), _currentDeltaT(initialDeltaT), _current2N(abs(2 * n)), _currentDeltaTBuffer(0)
{
	this->_isDeceleration = n < 0;
	if (this->_isDeceleration && abs(n) < stepCount) {
		Serial.print('X');
		this->_remainingSteps = 0;
	}
}

void AccelerationPlan::loadFrom(byte * buffer)
{
	int16_t stepCount = READ_INT16(buffer, 0);
	uint16_t initialDeltaT = READ_UINT16(buffer, 2);
	int16_t n = READ_INT16(buffer, 2 + 2);

	this->_remainingSteps = abs(stepCount);
	this->_nextStepDirection = stepCount > 0;
	this->_isActive = true;
	this->_skipNextActivation = false;
	this->_nextActivationTime = 0;
	this->_nextDeactivationTime = -1;

	this->_isDeceleration = n < 0;
	this->_currentDeltaT = initialDeltaT;
	this->_current2N = abs(2 * n);
	this->_currentDeltaTBuffer = 0;

	if (this->_isDeceleration && abs(n) < stepCount) {
		Serial.print('X');
		this->_remainingSteps = 0;
	}
}

void AccelerationPlan::_createNextActivation()
{
	if (this->_remainingSteps == 0) {
		this->_isActive = false;
		return;
	}

	--this->_remainingSteps;

	uint16_t nextDeltaT = this->_currentDeltaT;

	if (this->_isDeceleration) {

		this->_currentDeltaTBuffer += nextDeltaT;
		while (this->_currentDeltaTBuffer > this->_current2N) {
			this->_currentDeltaTBuffer -= this->_current2N;
			nextDeltaT += 1;
		}

		//we increnemnt 2N by 2 to avoid multiplication
		this->_current2N -= 2;
	}
	else {
		//this is sligthly simplified in comparison to real taylor
		this->_currentDeltaTBuffer += nextDeltaT;
		while (this->_currentDeltaTBuffer > this->_current2N) {
			this->_currentDeltaTBuffer -= this->_current2N;
			nextDeltaT -= 1;
		}

		//we increnemnt 2N by 2 to avoid multiplication
		this->_current2N += 2;
	}
	this->_currentDeltaT = nextDeltaT;

	this->_nextActivationTime = this->_currentDeltaT;
}

ConstantPlan::ConstantPlan(int16_t stepCount, uint16_t baseDeltaT, uint16_t periodNumerator, uint16_t periodDenominator)
	:Plan(stepCount), _baseDeltaT(baseDeltaT), _periodNumerator(periodNumerator), _periodDenominator(periodDenominator), _periodAccumulator(0)
{
}

void ConstantPlan::loadFrom(byte * buffer)
{
	int16_t stepCount = READ_INT16(buffer, 0);
	uint16_t baseDeltaT = READ_UINT16(buffer, 2);
	uint16_t periodNumerator = READ_UINT16(buffer, 2 + 2);
	uint16_t periodDenominator = READ_UINT16(buffer, 2 + 2 + 2);

	this->_remainingSteps = abs(stepCount);
	this->_nextStepDirection = stepCount > 0;
	this->_isActive = true;
	this->_skipNextActivation = false;
	this->_nextActivationTime = 0;
	this->_nextDeactivationTime = -1;

	this->_baseDeltaT = baseDeltaT;
	this->_periodNumerator = periodNumerator;
	this->_periodDenominator = periodDenominator;
	this->_periodAccumulator = 0;
}

void ConstantPlan::_createNextActivation()
{
	if (this->_remainingSteps == 0) {
		this->_isActive = false;
		return;
	}

	--this->_remainingSteps;

	uint16_t currentDelta = this->_baseDeltaT;

	if (this->_periodNumerator > 0) {
		this->_periodAccumulator += this->_periodNumerator;
		if (this->_periodDenominator >= this->_periodAccumulator) {
			this->_periodAccumulator -= this->_periodDenominator;
			currentDelta += 1;
		}
	}

	this->_nextActivationTime = currentDelta;
}

