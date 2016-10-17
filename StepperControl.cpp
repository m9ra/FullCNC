#include "StepperControl.h"


#define B_LOW(y) (PORTB&=(~(1<<y)))
#define B_HIGH(y) (PORTB|=(1<<y))

#define SCHEDULE_BUFFER_LEN 256

// buffer for step signal timing
uint16_t SCHEDULE_BUFFER[SCHEDULE_BUFFER_LEN + 1];
// bitwise activation mask for step signals (selecting active ports)
byte SCHEDULE_ACTIVATIONS[SCHEDULE_BUFFER_LEN + 1];

volatile byte SCHEDULE_ACTIVATIONS_MASK = 3;
// bitwise mask selecting only dir related ports
volatile byte SCHEDULE_DIR_MASK = 2;
// bitwise mask selecting only clock related ports
volatile byte SCHEDULE_CLK_MASK = 1;

// determine whether schedule has next activations available
bool HAS_NEXT_ACTIVATIONS = true;
// pointer where new timing will be stored
volatile byte SCHEDULE_START = 0;
// pointer where scheduler is actually reading
volatile byte SCHEDULE_END = 0;
// the precomputed time for scheduler's next turn
volatile uint16_t NEXT_SCHEDULED_TIME = 0;
// activations precomputed for next scheduler's turn
volatile byte NEXT_SCHEDULED_ACTIVATIONS = 0;
// activations for current scheduler's turn
volatile byte CURRENT_SCHEDULED_ACTIVATIONS = 0;

ISR(TIMER1_OVF_vect) {
	TCNT1 = NEXT_SCHEDULED_TIME;

	//TODO make something about those delays!!!!

	PORTB = SCHEDULE_CLK_MASK | (SCHEDULE_DIR_MASK & CURRENT_SCHEDULED_ACTIVATIONS);

	delayMicroseconds(5);
	PORTB = CURRENT_SCHEDULED_ACTIVATIONS;

	//delayMicroseconds(3);

	CURRENT_SCHEDULED_ACTIVATIONS = NEXT_SCHEDULED_ACTIVATIONS;
	if (SCHEDULE_START == SCHEDULE_END) {
		if (HAS_NEXT_ACTIVATIONS) {
			//one step still remains in current next scheduled activations
			HAS_NEXT_ACTIVATIONS = false;
			NEXT_SCHEDULED_ACTIVATIONS = SCHEDULE_CLK_MASK;
			NEXT_SCHEDULED_TIME = 0;
		}
		else {// we have schedule stream end
			// stop scheduling after this step
			TIMSK1 = 0;
		}
	}
	else {
		NEXT_SCHEDULED_TIME = SCHEDULE_BUFFER[SCHEDULE_END];
		NEXT_SCHEDULED_ACTIVATIONS = SCHEDULE_ACTIVATIONS[SCHEDULE_END++];
		HAS_NEXT_ACTIVATIONS = true;
	}
	//PORTB = SCHEDULE_CLK_MASK | (SCHEDULE_DIR_MASK & CURRENT_SCHEDULED_ACTIVATIONS);
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

	CURRENT_SCHEDULED_ACTIVATIONS = SCHEDULE_CLK_MASK; //we are starting with empty schedule
	NEXT_SCHEDULED_TIME = SCHEDULE_BUFFER[SCHEDULE_END];
	NEXT_SCHEDULED_ACTIVATIONS = SCHEDULE_ACTIVATIONS[SCHEDULE_END++];
	HAS_NEXT_ACTIVATIONS = true;

	Serial.print('S'); //enabling scheduler

	TCNT1 = 65535; //wake up scheduler as early as possible
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
		uint16_t earliestScheduleTime = 65535;
		for (int i = 0; i < group.StepperCount; ++i) {
			uint16_t nextScheduleTime = plans[i]->_nextScheduleTime;
			earliestScheduleTime = min(earliestScheduleTime, nextScheduleTime);
		}

		//subtract deltaT from other plans
		byte clockMask = SCHEDULE_CLK_MASK;
		bool hasActivePlan = false;
		for (int i = 0; i < group.StepperCount; ++i) {
			Plan* plan = plans[i];
			if (!plan->_isActive)
				continue;

			hasActivePlan = true;
			plan->_nextScheduleTime -= earliestScheduleTime;
			if (plan->_nextScheduleTime == 0) {
				//plan has to be scheduled
				clockMask &= ~group._clockBports[i];
				if (plan->stepDirection > 0)
					clockMask |= group._dirBports[i];
				//compute new schedule time for plan
				plan->_nextScheduleTime = plan->_createNextDeltaT();
				if (plan->_nextScheduleTime == 0)
					//it is plan over
					plan->_isActive = false;
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

		SCHEDULE_BUFFER[SCHEDULE_START] = 65535 - earliestScheduleTime;
		SCHEDULE_ACTIVATIONS[SCHEDULE_START++] = clockMask;

		if ((byte)(SCHEDULE_START + 1) == SCHEDULE_END)
			//we have free time
			return true;
	}
	Steppers::startScheduler();
	return false;
}



void Steppers::initPlanning(StepperGroup & group, Plan ** plans)
{
	//initialize port info
	byte dirPorts = 0;
	byte clockMask = 0;
	byte dirMask = 0;

	//TODO
	//SCHEDULE_ACTIVATIONS_MASK = 3;
	for (int i = 0; i < group.StepperCount; ++i) {
		plans[i]->_nextScheduleTime = plans[i]->_createNextDeltaT();
		//dirMask |= group._dirBports[i];
	}

	//TODO ensure that this remains unchanged between scheduler runs (CANNOT BE CHANGED WITHOUT SCHEDULER DISABLING)
	//SCHEDULE_DIR_MASK = dirMask;
}

void Steppers::directScheduleFill(byte* activations, int16_t* timing, int count) {
	for (int i = 0; i < count; ++i) {
		byte activation = activations[i];
		int16_t time = timing[i];

		if ((byte)(SCHEDULE_START + 1) == SCHEDULE_END) {
			Serial.println(F("|Not space for direct schedule"));
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
	stepDirection(stepCount > 0 ? 1 : 0), _isActive(true), _nextScheduleTime(0), _remainingSteps(abs(stepCount))
{
}

AccelerationPlan::AccelerationPlan(int16_t stepCount, uint16_t initialDeltaT, int16_t n)
	: Plan(stepCount), _currentDeltaT(initialDeltaT), _current2N(abs(2 * n)), _currentDeltaTBuffer(0)
{
	this->_isDeceleration = n < 0;
	if (this->_isDeceleration && abs(n) < stepCount) {
		Serial.println(F("|Acceleration plan is too long!"));
		this->_remainingSteps = 0;
	}
}

uint16_t AccelerationPlan::_createNextDeltaT()
{
	if (this->_remainingSteps == 0)
		return 0;

	--this->_remainingSteps;

	uint16_t nextDeltaT = this->_currentDeltaT;

	if (this->_isDeceleration) {

		this->_currentDeltaTBuffer += nextDeltaT;
		while (this->_currentDeltaTBuffer > this->_current2N) {
			this->_currentDeltaTBuffer -= this->_current2N;
			nextDeltaT += 1;
		}
		/*Serial.print("|D");
		Serial.println(nextDeltaT);*/
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

		/*Serial.print("|A");
		Serial.println(nextDeltaT);*/
		//we increnemnt 2N by 2 to avoid multiplication
		this->_current2N += 2;
	}
	this->_currentDeltaT = nextDeltaT;

	return this->_currentDeltaT;
}

ConstantPlan::ConstantPlan(int16_t stepCount, uint16_t baseDeltaT, uint16_t periodNumerator, uint16_t periodDenominator)
	:Plan(stepCount), _baseDeltaT(baseDeltaT), _periodNumerator(periodNumerator), _periodDenominator(periodDenominator), _periodAccumulator(0)
{
}

uint16_t ConstantPlan::_createNextDeltaT()
{
	if (this->_remainingSteps == 0)
		return 0;


	uint16_t currentDelta = this->_baseDeltaT;

	this->_periodAccumulator += this->_periodNumerator;
	if (this->_periodDenominator >= this->_periodAccumulator) {
		this->_periodAccumulator -= this->_periodDenominator;
		currentDelta += 1;
	}

	--this->_remainingSteps;

	return currentDelta;
}

