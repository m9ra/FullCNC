#include "StepperControl.h"

uint16_t SCHEDULE_BUFFER[SCHEDULE_BUFFER_LEN + 1];
byte SCHEDULE_ACTIVATIONS[SCHEDULE_BUFFER_LEN + 1];
byte CUMULATIVE_SCHEDULE_ACTIVATION = 0;

volatile byte SCHEDULE_START = 0;
volatile byte SCHEDULE_END = 0;

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


AccelerationPlan::AccelerationPlan(byte clkPin, byte dirPin)
	: Plan(clkPin, dirPin), _currentDeltaT(0), _current2N(0), _currentDeltaTBuffer(0), _isDeceleration(false)
{
}

void AccelerationPlan::loadFrom(byte * data)
{
	int16_t stepCount = READ_INT16(data, 0);
	uint16_t initialDeltaT = READ_UINT16(data, 2);
	int16_t n = READ_INT16(data, 2 + 2);

	this->remainingSteps = abs(stepCount);
	this->stepMask = stepCount > 0 ? this->dirMask : 0;

	this->_isDeceleration = n < 0;
	this->_currentDeltaT = initialDeltaT;
	this->_current2N = abs(2 * n);
	this->_currentDeltaTBuffer = 0;

	if (this->_isDeceleration && abs(n) < stepCount) {
		Serial.print('X');
		this->remainingSteps = 0;
	}
}

void AccelerationPlan::createNextActivation()
{
	if (this->remainingSteps == 0) {
		this->isActive = false;
		return;
	}

	--this->remainingSteps;

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

	this->nextActivationTime = this->_currentDeltaT;
}

void ConstantPlan::createNextActivation()
{
	if (this->remainingSteps == 0) {
		this->isActive = false;
		return;
	}

	--this->remainingSteps;

	uint16_t currentDeltaT = this->_baseDeltaT;

	if (this->_periodNumerator > 0) {
		this->_periodAccumulator += this->_periodNumerator;
		if (this->_periodDenominator >= this->_periodAccumulator) {
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
	uint16_t _baseDeltaT = READ_UINT16(data, 2);
	uint16_t _periodNumerator = READ_UINT16(data, 2 + 2);
	uint16_t _periodDenominator = READ_UINT16(data, 2 + 2 + 2);

	this->remainingSteps = abs(stepCount);
	this->stepMask = stepCount > 0 ? this->dirMask : 0;

	this->_baseDeltaT = _baseDeltaT;
	this->_periodNumerator = _periodNumerator;
	this->_periodDenominator = _periodDenominator;
	this->_periodAccumulator = 0;
}

Plan::Plan(byte clkPin, byte dirPin) :
	clkMask(PIN_TO_MASK(clkPin)), dirMask(PIN_TO_MASK(dirPin)),
	stepMask(0), remainingSteps(0), isActive(false), nextActivationTime(0)
{
}
