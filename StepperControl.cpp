#include "StepperControl.h"


#define B_LOW(y) (PORTB&=(~(1<<y)))
#define B_HIGH(y) (PORTB|=(1<<y))

#define SCHEDULE_BUFFER_LEN 256

#define ACCELERATION_DISTANCE(deltaT) ACCELERATION_TABLE[deltaT-MIN_DELTA_T]

// precomputed acceleration table
uint16_t ACCELERATION_TABLE[START_DELTA_T - MIN_DELTA_T + 1];

// buffer for step signal timing
uint16_t SCHEDULE_BUFFER[SCHEDULE_BUFFER_LEN + 1];
// bitwise activation mask for step signals (selecting active ports)
byte SCHEDULE_ACTIVATIONS[SCHEDULE_BUFFER_LEN + 1];
// bitwise mask selecting only related ports
byte SCHEDULE_ACTIVATIONS_MASK;

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

void initializeAccelerationTable() {
	// Precomputes acceleration timings.

	for (int deltaT = START_DELTA_T; deltaT >= MIN_DELTA_T; --deltaT) {
		long s = (uint64_t)TIMESCALE * (uint64_t)TIMESCALE / MAX_ACCELERATION / STEPS_PER_REVOLUTION / deltaT / deltaT / 2;
		Serial.print(deltaT);
		Serial.print(" ");
		Serial.println(s);

		ACCELERATION_TABLE[deltaT - MIN_DELTA_T] = s;
	}
}

ISR(TIMER1_OVF_vect) {
	TCNT1 = NEXT_SCHEDULED_TIME;
	PORTB |= CURRENT_SCHEDULED_ACTIVATIONS;
	
	if (SCHEDULE_START == SCHEDULE_END) {		
		if (NEXT_SCHEDULED_TIME == 65535) {
			// we have schedule stream end
			// stop scheduling after this step
			TIMSK1 = 0;
		}
		else{
			//TODO this shouts for being improved
			//one step still remains in current next scheduled activations
			NEXT_SCHEDULED_TIME = 65535;
			CURRENT_SCHEDULED_ACTIVATIONS = NEXT_SCHEDULED_ACTIVATIONS;
		}
	}
	else {
		CURRENT_SCHEDULED_ACTIVATIONS = NEXT_SCHEDULED_ACTIVATIONS;
		NEXT_SCHEDULED_TIME = SCHEDULE_BUFFER[SCHEDULE_END];
		NEXT_SCHEDULED_ACTIVATIONS = SCHEDULE_ACTIVATIONS[SCHEDULE_END++];
	}
	PORTB &= ~SCHEDULE_ACTIVATIONS_MASK;
}

void Steppers::initialize()
{
	initializeAccelerationTable();

	noInterrupts(); // disable all interrupts
	TCCR1A = 0;
	TCCR1B = 0;
	TIMSK1 = 0;

	TCCR1B |= 1 << CS11; // 8 prescaler
	interrupts(); // enable all interrupts
}


void ensureSchedulerEnabled() {
	if (TIMSK1 != 0)
		//scheduler is running
		return;

	if (SCHEDULE_START == SCHEDULE_END)
		//schedule is empty - no point in schedule enabling
		return;

	CURRENT_SCHEDULED_ACTIVATIONS = 0; //we are starting with empty schedule
	NEXT_SCHEDULED_TIME = SCHEDULE_BUFFER[SCHEDULE_END];
	NEXT_SCHEDULED_ACTIVATIONS = SCHEDULE_ACTIVATIONS[SCHEDULE_END++];

	Serial.println("Enabling scheduler");
	Serial.flush();

	TCNT1 = 65535; //schedule call will cause immediate step
	TIMSK1 = (1 << TOIE1); //enable scheduler
}



void Steppers::runPlanning(StepperGroup & group, Plan ** plans)
{
	//initialize port info
	byte dirPorts = 0;
	byte clockMask = 0;
	for (int i = 0; i < group.StepperCount; ++i) {
		//preset direction ports
		if (plans[i]->stepDirection > 0)
			B_HIGH(group._dirBports[i]);
		else
			B_LOW(group._dirBports[i]);

		plans[i]->_nextScheduleTime = plans[i]->_createNextDeltaT();

		clockMask |= group._clockBports[i];
	}

	//TODO ensure that this remains unchanged between scheduler runs (CANNOT BE CHANGED WITHOUT SCHEDULER DISABLING)
	SCHEDULE_ACTIVATIONS_MASK = clockMask;
	
	for (;;) {
		//find earliest plan
		uint16_t earliestScheduleTime = 65535;
		for (int i = 0; i < group.StepperCount; ++i) {
			uint16_t nextScheduleTime = plans[i]->_nextScheduleTime;
			earliestScheduleTime = min(earliestScheduleTime, nextScheduleTime);
		}

		//subtract deltaT from other plans
		byte clockMask = 0;
		bool hasActivePlan = false;
		for (int i = 0; i < group.StepperCount; ++i) {
			Plan* plan = plans[i];
			if (!plan->_isActive)
				continue;

			hasActivePlan = true;
			plan->_nextScheduleTime -= earliestScheduleTime;
			if (plan->_nextScheduleTime == 0) {
				//plan has to be scheduled
				clockMask |= group._clockBports[i];

				//compute new schedule time for plan
				plan->_nextScheduleTime = plan->_createNextDeltaT();
				if (plan->_nextScheduleTime == 0)
					//it is plan over
					plan->_isActive = false;
			}
		}

		if (!hasActivePlan)
			//there is not any active plan
			break;

		//schedule
		byte scheduleStart = SCHEDULE_START + 1;
		while (scheduleStart == SCHEDULE_END) {
			ensureSchedulerEnabled();//wait until schedule buffer has empty space
		}

		SCHEDULE_BUFFER[SCHEDULE_START] = 65535 - earliestScheduleTime;
		SCHEDULE_ACTIVATIONS[SCHEDULE_START++] = clockMask;
	}
	ensureSchedulerEnabled();
	Serial.println("Deleting plans.");
	Serial.flush();
	//free plan memory
	for (int i = 0; i < group.StepperCount; ++i)
		delete plans[i];

	delete plans;
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
	stepDirection(stepCount > 0 ? 1 : 0), _isActive(true), _nextScheduleTime(0)
{
}

AccelerationPlan::AccelerationPlan(int16_t stepCount, int16_t accelerationNumerator, int16_t accelerationDenominator, uint16_t initialDelta)
	: Plan(stepCount), _remainingSteps(abs(stepCount)), _accelerationNumerator(abs(accelerationNumerator)), _accelerationDenominator(abs(accelerationDenominator)),
	_accelerationNumeratorBuffer(0),
	_currentDeltaT(initialDelta)
{
	this->_isDeceleration = accelerationNumerator >= 0 != accelerationDenominator >= 0;
	if (this->_isDeceleration) {
		_accelerationNumeratorBuffer = abs(accelerationNumerator)*_remainingSteps;
	}
}

uint16_t AccelerationPlan::_createNextDeltaT()
{
	if (this->_remainingSteps == 0)
		return 0;

	//keep track of acceleration factor
	int16_t currentDeltaT = this->_currentDeltaT;

	if (this->_isDeceleration) {
		this->_accelerationNumeratorBuffer -= this->_accelerationNumerator;
		//underflow will stop further deceleration (which is on purpose - the second underflow cannot happen - is bounded by stepcount)
		while (currentDeltaT < START_DELTA_T &&
			this->_accelerationNumeratorBuffer < (uint32_t)ACCELERATION_DISTANCE(currentDeltaT) * this->_accelerationDenominator) {
			++currentDeltaT;
		}
	}
	else {
		this->_accelerationNumeratorBuffer += this->_accelerationNumerator;
		while (currentDeltaT > MIN_DELTA_T &&
			this->_accelerationNumeratorBuffer > (uint32_t)ACCELERATION_DISTANCE(currentDeltaT) * this->_accelerationDenominator) {
			--currentDeltaT;
		}
	}

	--this->_remainingSteps;
	this->_currentDeltaT = currentDeltaT;
	return 2 * currentDeltaT;
}

ConstantPlan::ConstantPlan(int16_t stepCount, uint16_t baseDeltaT, uint16_t remainderPeriod)
	:Plan(stepCount),
	_remainingSteps(abs(stepCount)), _baseDeltaT(baseDeltaT), _remainderPeriod(remainderPeriod)
{
	_currentRemainderOffset = _remainderPeriod / 2;
}

uint16_t ConstantPlan::_createNextDeltaT()
{
	if (this->_remainingSteps == 0)
		return 0;

	if (this->_remainderPeriod > 0)
		--this->_currentRemainderOffset;

	--this->_remainingSteps;

	if (this->_currentRemainderOffset < 0) {
		// we have to distribute remainder evenly
		this->_currentRemainderOffset = this->_remainderPeriod;
		return this->_baseDeltaT * 2 + 2;
	}
	else {
		return this->_baseDeltaT * 2;
	}
}

