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

// determine whether schedule has next activations available
volatile bool HAS_NEXT_ACTIVATIONS = true;
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
	PORTB = CURRENT_SCHEDULED_ACTIVATIONS;

	CURRENT_SCHEDULED_ACTIVATIONS = NEXT_SCHEDULED_ACTIVATIONS;
	if (SCHEDULE_START == SCHEDULE_END) {
		if (HAS_NEXT_ACTIVATIONS) {
			//one step still remains in current scheduled activations
			HAS_NEXT_ACTIVATIONS = false;
			NEXT_SCHEDULED_TIME = 0;
		}
		else {
			// we have schedule stream end
			// stop scheduling after this step
			TIMSK1 = 0;
		}
	}
	else {
		NEXT_SCHEDULED_TIME = SCHEDULE_BUFFER[SCHEDULE_END];
		NEXT_SCHEDULED_ACTIVATIONS = SCHEDULE_ACTIVATIONS[SCHEDULE_END++];
		HAS_NEXT_ACTIVATIONS = true;
	}
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

	CURRENT_SCHEDULED_ACTIVATIONS = PORTB; //we won't change activations by first iteration
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
		uint16_t earliestActivationTime = 65535;
		for (int i = 0; i < group.StepperCount; ++i) {
			uint16_t nextActivationTime = plans[i]->_nextActivationTime;
			earliestActivationTime = min(earliestActivationTime, nextActivationTime);
		}

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
				byte combinedRevMask = ~(plan->_clockMask | plan->_dirMask);
				CUMULATIVE_SCHEDULE_ACTIVATION = (CUMULATIVE_SCHEDULE_ACTIVATION & combinedRevMask) | plan->_nextActivation;
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
		SCHEDULE_ACTIVATIONS[SCHEDULE_START++] = CUMULATIVE_SCHEDULE_ACTIVATION;

		/*	Serial.print("| t:");
			Serial.print(earliestActivationTime);
			Serial.print(", a:");
			Serial.println(CUMULATIVE_SCHEDULE_ACTIVATION);*/

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
		plans[i]->_dirMask = group._dirBports[i];
		plans[i]->_clockMask = group._clockBports[i];

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
	stepDirection(stepCount > 0 ? 1 : 0), _remainingSteps(abs(stepCount)),
	_isActive(true), _dirMask(0), _clockMask(0),
	_isPulseStartPhase(true), _isDirReported(false),
	_nextActivationTime(0), _nextActivation(0)
{
}

void Plan::_reportDir()
{
	this->_isDirReported = true;
	this->_nextActivationTime = PORT_CHANGE_DELAY;
	this->_nextActivation = this->_clockMask;
	if (this->stepDirection > 0)
		this->_nextActivation |= this->_dirMask;
}

void Plan::_reportPulseEnd()
{
	this->_isPulseStartPhase = true;
	this->_nextActivationTime = PORT_CHANGE_DELAY;
	this->_nextActivation = this->_clockMask;
	if (this->stepDirection > 0)
		this->_nextActivation |= this->_dirMask;
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

void AccelerationPlan::_createNextActivation()
{
	if (this->_remainingSteps == 0) {
		this->_isActive = false;
		return;
	}

	if (!this->_isDirReported) {
		this->_reportDir();
		return;
	}

	if (!this->_isPulseStartPhase) {
		this->_reportPulseEnd();
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

	this->_isPulseStartPhase = false;
	this->_nextActivationTime = this->_currentDeltaT - PORT_CHANGE_DELAY;
	this->_nextActivation = this->stepDirection > 0 ? this->_dirMask : 0;
}

ConstantPlan::ConstantPlan(int16_t stepCount, uint16_t baseDeltaT, uint16_t periodNumerator, uint16_t periodDenominator)
	:Plan(stepCount), _baseDeltaT(baseDeltaT), _periodNumerator(periodNumerator), _periodDenominator(periodDenominator), _periodAccumulator(0)
{
}

void ConstantPlan::_createNextActivation()
{
	if (this->_remainingSteps == 0) {
		this->_isActive = false;
		return;
	}

	if (!this->_isDirReported) {
		this->_reportDir();
		return;
	}

	if (!this->_isPulseStartPhase) {
		this->_reportPulseEnd();
		return;
	}

	--this->_remainingSteps;

	uint16_t currentDelta = this->_baseDeltaT;

	//if (this->_periodNumerator > 0) {
	this->_periodAccumulator += this->_periodNumerator;
	if (this->_periodDenominator >= this->_periodAccumulator) {
		this->_periodAccumulator -= this->_periodDenominator;
		currentDelta += 1;
	}
	//}


	this->_isPulseStartPhase = false;
	this->_nextActivationTime = currentDelta - PORT_CHANGE_DELAY;
	this->_nextActivation = this->stepDirection > 0 ? this->_dirMask : 0;
}

