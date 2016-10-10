#include "StepperControl.h"

bool Steppers::_active_period = false;
Stepper* Steppers::_first_stepper = NULL;
bool Steppers::_is_initialized = false;


#define B_HIGH(y) (PORTB&=(~(1<<y)))

#define B_LOW(y) (PORTB|=(1<<y))

#define SCHEDULE_BUFFER_LEN 256

uint16_t ACCELERATION_TABLE[START_DELTA_T - MIN_DELTA_T];
uint16_t SCHEDULE_BUFFER[SCHEDULE_BUFFER_LEN + 1];

volatile byte SCHEDULE_START = 0;
volatile byte SCHEDULE_END = 0;
volatile uint16_t NEXT_SCHEDULED_TIME = 0;


void initializeAccelerationTable() {
	for (int deltaT = START_DELTA_T; deltaT >= MIN_DELTA_T; --deltaT) {
		long s = (uint64_t)TIMESCALE * (uint64_t)TIMESCALE / MAX_ACCELERATION / STEPS_PER_REVOLUTION / deltaT / deltaT / 2;
		Serial.print(deltaT);
		Serial.print(" ");
		Serial.println(s);

		ACCELERATION_TABLE[deltaT - MIN_DELTA_T] = s;
	}
}

ISR(TIMER1_OVF_vect) {
	TCNT1 = 65535 - NEXT_SCHEDULED_TIME;
	B_LOW(0);
	B_HIGH(0);
	if (SCHEDULE_START == SCHEDULE_END) {
		// we have schedule stream end
		// stop scheduling after this step
		TIMSK1 = 0;
	}
	else {
		NEXT_SCHEDULED_TIME = SCHEDULE_BUFFER[SCHEDULE_END++];
	}

}


void Steppers::initialize()
{
	initializeAccelerationTable();

	noInterrupts(); // disable all interrupts
	TCCR1A = 0;
	TCCR1B = 0;
	TIMSK1 = 0;

	//TCNT1 = 34286; // preload timer 65536-16MHz/256/2Hz
	TCCR1B |= 1 << CS11; // 8 prescaler
	interrupts(); // enable all interrupts

	Stepper* stepper = Steppers::_first_stepper;
	while (stepper != NULL) {
		stepper->_initialize();
		stepper = stepper->_next_stepper;
	}
}

void ensureScheduleEnabled() {
	if (TIMSK1 != 0)
		//scheduler is running
		return;

	if (SCHEDULE_START == SCHEDULE_END)
		//schedule is empty - no point in schedule enabling
		return;

	//=========UNFORTUNATELY WE HAVE TO UNWIND PART OF THE SCHEDULER HERE=============
	uint16_t firstScheduledTime = SCHEDULE_BUFFER[SCHEDULE_END++];
	Serial.println("Enabling scheduler");
	Serial.flush();

	TCNT1 = 65535 - firstScheduledTime; //schedule call will cause immediate step
	//============================================================================
	TIMSK1 = (1 << TOIE1); //enable scheduler
}

void Steppers::runPlanning(Stepper & stepper, Plan & plan)
{
	for (;;) {
		int16_t nextDeltaT = plan._createNextDeltaT();
		if (nextDeltaT == 0) {
			Serial.println("Plan end");
			Serial.flush();
			break;
		}

		byte scheduleStart = SCHEDULE_START + 1;
		while (scheduleStart == SCHEDULE_END) {
			ensureScheduleEnabled();//wait until schedule buffer has empty space
		}

		SCHEDULE_BUFFER[SCHEDULE_START] = nextDeltaT;
		++SCHEDULE_START;
	}
	ensureScheduleEnabled();
}

int32_t Steppers::calculateAccelerationDistance(Fraction32& startVelocity, Fraction32 & accelerationCoefficient, Fraction32 & targetVelocity)
{
	Serial.println("acceleration distance");
	uint16_t startDelta = (Fraction32(TIMESCALE) / startVelocity).to_int32_t();
	uint16_t targetDelta = (Fraction32(TIMESCALE) / targetVelocity).to_int32_t();

	startDelta = CLIP_D(startDelta);
	targetDelta = CLIP_D(targetDelta);
	Serial.println(startDelta);
	Serial.println(targetDelta);
	int32_t distance = ACCELERATION_TABLE[targetDelta - MIN_DELTA_T] - ACCELERATION_TABLE[startDelta - MIN_DELTA_T];
	Serial.println(distance);
	return (Fraction32(distance) / accelerationCoefficient).to_int32_t();
}

Stepper & Steppers::createStepper(int pinClk, int pinDir, int inertia)
{
	Stepper* stepper;
	if (Steppers::_is_initialized) {
		Serial.write("ERROR: Cannot create steppers after initialization");
		stepper = NULL;
	}
	else {
		stepper = new Stepper(pinClk, pinDir, inertia);
		stepper->_next_stepper = Steppers::_first_stepper;
		Steppers::_first_stepper = stepper;
	}

	return *stepper;
}

StepPlan::StepPlan(int count, int delay) :count(count), delay(delay), _next_plan(NULL)
{
}

Stepper::Stepper(int pinClk, int pinDir, int inertia) : _pin_clk(pinClk), _pin_dir(pinDir), _inertia(inertia),
_last_step_iterations(1000), _planProgress(0), _delayProgress(0),
_next_plan(NULL), _active_plan(NULL), _next_stepper(NULL), _last_plan(NULL)
{
}

void Stepper::step(int count, int delay)
{
	StepPlan* plan = new StepPlan(count, delay);
	cli();
	if (this->_last_plan == NULL) {
		this->_next_plan = plan;
		this->_last_plan = plan;
	}
	else {
		this->_last_plan->_next_plan = plan;
		this->_last_plan = plan;
	}
	sei();
}

bool Stepper::isBusy()
{
	return this->_active_plan != NULL || this->_next_plan != NULL;
}

void Stepper::_initialize()
{
	pinMode(this->_pin_clk, OUTPUT);
	pinMode(this->_pin_dir, OUTPUT);
}

void Stepper::_controlPulse(bool activePeriod)
{
	if (!activePeriod) {
		digitalWrite(this->_pin_clk, LOW);
		return;
	}

	_last_step_iterations += 1;
	if (_last_step_iterations > 1000)
		_last_step_iterations = 1000;

	if (this->_planProgress <= 0 && this->_active_plan != NULL) {
		//cleanup the plan because it was completed already
		delete this->_active_plan;
		this->_active_plan = NULL;
	}

	if (this->_active_plan == NULL) {
		//we have to set a new plan
		if (this->_next_plan == NULL)
			//there is no new plan
			return;

		this->_active_plan = this->_next_plan;
		this->_next_plan = this->_active_plan->_next_plan;
		if (this->_next_plan == NULL)
			//it was the last plan
			this->_last_plan = NULL;

		this->_planProgress = abs(this->_active_plan->count);
		this->_delayProgress = 0;
		int dir = this->_active_plan->count < 0 ? 0 : 1;
		digitalWrite(this->_pin_dir, dir);
	}

	if (this->_delayProgress <= 0) {
		digitalWrite(this->_pin_clk, HIGH);
		this->_planProgress -= 1;

		int staticSpeed = 6;
		int speed = this->_active_plan->delay;

		int stepCount = abs(this->_active_plan->count);
		int i = stepCount - this->_planProgress;
		int acceleration = 3;

		int rampStart = max(0, staticSpeed - speed - i / acceleration);
		int rampEnd = max(0, staticSpeed - speed - (stepCount - i) / acceleration);

		this->_delayProgress = max(5, this->_active_plan->delay) + rampStart + rampEnd;

	}
	else {
		this->_delayProgress -= 1;
	}
}

AccelerationPlan::AccelerationPlan(Fraction32 & accelerationCoefficient, uint16_t initialDeltaT, int32_t stepCount) : Plan(stepCount),
_accelerationCoefficient(accelerationCoefficient.absolute()), _currentDeltaT(max(MIN_DELTA_T, min(START_DELTA_T, initialDeltaT))), _remainingSteps(abs(stepCount))
, _accelerationIncrement(accelerationCoefficient.Numerator < 0 ? -1 : 1)
{
	this->_doneTrack = ACCELERATION_TABLE[initialDeltaT - MIN_DELTA_T];
}

uint16_t AccelerationPlan::_createNextDeltaT()
{
	if (this->_remainingSteps == 0)
		return 0;

	--this->_remainingSteps;

	uint32_t denominator = this->_accelerationCoefficient.Denominator;
	uint32_t numerator = this->_accelerationCoefficient.Numerator;
	if (this->_accelerationIncrement < 0) {
		if (this->_currentDeltaT < START_DELTA_T &&
			this->_remainingSteps*numerator < ACCELERATION_TABLE[this->_currentDeltaT - MIN_DELTA_T] * denominator)
		{
			++this->_currentDeltaT;
		}
	}
	else {
		//Serial.println(this->_currentDeltaT);

		if (this->_currentDeltaT > MIN_DELTA_T &&
			this->_doneTrack *numerator > ACCELERATION_TABLE[this->_currentDeltaT - MIN_DELTA_T] * denominator)
		{
			--this->_currentDeltaT;

		}
	}

	this->_doneTrack += this->_accelerationIncrement;
	return this->_currentDeltaT * 2;
}

Plan::Plan(int32_t stepCount) :
	stepDirection(stepCount > 0 ? 1 : 0)
{
}

ConstantPlan::ConstantPlan(int32_t stepCount, int32_t totalTime) : Plan(stepCount),
_remainingSteps(max(0, stepCount)),
_baseDeltaT(totalTime / stepCount),
_remainderPeriod(stepCount / (totalTime % stepCount))
{
	Serial.println("constant plan");
	Serial.println(totalTime);
	Serial.println(_baseDeltaT);
	this->_currentRemainderOffset = this->_remainderPeriod / 2;
}

uint16_t ConstantPlan::_createNextDeltaT()
{
	if (this->_remainingSteps == 0)
		return 0;

	--this->_remainingSteps;

	if (this->_remainderPeriod > 0)
		--this->_currentRemainderOffset;

	if (this->_currentRemainderOffset < 0) {
		// we have to distribute remainder evenly
		this->_currentRemainderOffset = this->_remainderPeriod;
		return this->_baseDeltaT * 2 + 2;
	}
	else {
		return this->_baseDeltaT * 2;
	}
}

int32_t gcdr(int32_t a, int32_t b)
{
	if (a == 0) return b;
	return gcdr(b%a, a);
}

Fraction32::Fraction32(int32_t numerator, int32_t denominator)
	:Numerator(numerator), Denominator(numerator == 0 ? 1 : denominator)
{
	uint32_t gcd = gcdr(abs(numerator), abs(denominator));
	(uint32_t&)this->Numerator = numerator / gcd;
	(uint32_t&)this->Denominator = denominator / gcd;
}

int32_t Fraction32::to_int32_t()
{
	return this->Numerator / this->Denominator;
}

Fraction32 Fraction32::absolute()
{
	return Fraction32(abs(Numerator), abs(Denominator));
}

Fraction32 Fraction32::operator*(uint32_t factor)
{
	return Fraction32(Numerator*factor, Denominator);
}

Fraction32 Fraction32::operator+(Fraction32 op2)
{
	return Fraction32(this->Numerator*op2.Denominator + op2.Numerator*this->Denominator, this->Denominator*op2.Denominator);
}

Fraction32 Fraction32::operator-(Fraction32 op2)
{
	return Fraction32(this->Numerator*op2.Denominator - op2.Numerator*this->Denominator, this->Denominator*op2.Denominator);
}

Fraction32 Fraction32::operator*(Fraction32 op2)
{
	return Fraction32(this->Numerator*op2.Numerator, this->Denominator*op2.Denominator);
}

Fraction32 Fraction32::operator/(Fraction32 op2)
{
	return Fraction32(this->Numerator*op2.Denominator, this->Denominator*op2.Numerator);
}