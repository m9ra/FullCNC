/*
Name:		WindowManagerLib.h
Created:	6/10/2016 21:17:26
Author:	m9ra
Editor:	http://www.visualmicro.com
*/

#ifndef _StepperControl_h
#define _StepperControl_h

#if defined(ARDUINO) && ARDUINO >= 100
#include "arduino.h"
#else
#include "WProgram.h"
#endif

#if defined(__SAM3X8E__)
#undef __FlashStringHelper::F(string_literal)
#define F(string_literal) string_literal
#endif


#define READ_INT16(buff, position) (((int16_t)buff[(position)]) << 8) + buff[(position) + 1]
#define READ_UINT16(buff, position) (((uint16_t)buff[(position)]) << 8) + buff[(position) + 1]

#define MAX_ACCELERATION 200 //rev/s^2
#define START_DELTA_T 350 //us
#define MIN_DELTA_T 100	//us
#define STEPS_PER_REVOLUTION 400 //200 with 1/2 microstep
#define TIMESCALE 1000000 //us
#define CLIP_D(delta) max(MIN_DELTA_T,min(START_DELTA_T,delta))

//transforms pin to PORTB mask
#define PIN_TO_MASK(pinB)  (1 << (pinB - 8))

// how long (on 0.5us scale)
//	* before pulse the dir has to be specified 
//  * after pulse start pulse end has to be specified
// KEEPING BOTH VALUES SAME enables computation optimization
#define PORT_CHANGE_DELAY 5*2

// length of the schedule buffer (CANNOT be changed easily - it counts on byte overflows)
#define SCHEDULE_BUFFER_LEN 256

// buffer for step signal timing
extern uint16_t SCHEDULE_BUFFER[];
// bitwise activation mask for step signals (selecting active ports)
extern byte SCHEDULE_ACTIVATIONS[];

// pointer where new timing will be stored
extern volatile byte SCHEDULE_START;
// pointer where scheduler is actually reading
extern volatile byte SCHEDULE_END;
// cumulative activation with state up to lastly scheduled activation
extern byte CUMULATIVE_SCHEDULE_ACTIVATION;
//TODO load this during initialization
extern volatile byte ACTIVATIONS_CLOCK_MASK;

class Plan {
public:
	// Time of next scheduled activation
	uint16_t nextActivationTime;

	// How many steps remains to do with this plan.
	uint16_t remainingSteps;

	// Determine whether plan is still active.
	bool isActive;

	Plan(byte clkPin, byte dirPin);

	// Mask for clock port.
	const byte clkMask;
	// Mask for dir port.
	const byte dirMask;

	// OR Mask which is used for enhancing step activations about direction.
	byte stepMask;
};

class ConstantPlan : public Plan {
public:
	// How much data is required for load
	static const byte dataSize = 8;

	ConstantPlan(byte clkPin, byte dirPin);

	// Loads plan from given data.
	void loadFrom(byte* data);

	// Creates next activation.
	void createNextActivation();

private:
	// Base deltaT for step rate.
	uint16_t _baseDeltaT;
	// Period numerator for delay remainder displacement.
	uint16_t _periodNumerator;
	//Period denominator for delay remainder displacement.
	uint16_t _periodDenominator;
	//Period accumulator for remainder displacement.
	uint32_t _periodAccumulator;
};

class AccelerationPlan : public Plan {
public:
	// How much data is required for load.
	static const byte dataSize = 6;

	AccelerationPlan(byte clkPin, byte dirPin);

	// Loads plan from given data.
	void loadFrom(byte* data);

	// Creates next activation.
	void createNextActivation();
protected:
	// determine whether plan corresponds to deceleration
	bool _isDeceleration;
	// current n parameter of Taylor incremental acceleration formula
	uint32_t _current2N;
	// buffer for remainder accumulation
	uint32_t _currentDeltaTBuffer;
	// current deltaT which is used
	uint16_t _currentDeltaT;
};

class Steppers {
public:
	// Initialize registered steppers - no new steppers can be created afterewards.
	static void initialize();

	// Starts scheduler.
	static bool startScheduler();
private:
	// Determine whether steppers environment is initialized.
	static bool _isInitialized;
};


template<typename PlanType> class PlanScheduler2D {
public:
	PlanScheduler2D(byte clkPin1, byte dirPin1, byte clkPin2, byte dirPin2)
		:_d1(clkPin1, dirPin1), _d2(clkPin2, dirPin2), _needInit(false)
	{
	}

	// loads plan from given data
	void initFrom(byte * data)
	{
		this->_d1.loadFrom(data);
		this->_d2.loadFrom(data + _d1.dataSize);

		this->_d1.createNextActivation();
		this->_d2.createNextActivation();
		this->_needInit = true;
	}

	inline void triggerPlan(PlanType& plan, uint16_t nextActivationTime) {
		if (!plan.isActive)
			//there is nothing to do
			return;

		plan.nextActivationTime -= nextActivationTime;
		//TODO handle direction better

		if (plan.nextActivationTime != 0)
			//no steps for the plan now
			return;

		// make the appropriate pin LOW
		CUMULATIVE_SCHEDULE_ACTIVATION &= ~(plan.clkMask);

		//compute next activation
		plan.createNextActivation();
	}

	// fills schedule buffer with plan data
	// returns true when buffer is full (temporarly), false when plan is over
	bool fillSchedule() {

		while (_d1.isActive || _d2.isActive) {
			//find earliest plan
			uint16_t earliestActivationTime = min(_d1.nextActivationTime, _d2.nextActivationTime);
			if (_needInit) {
				earliestActivationTime = PORT_CHANGE_DELAY;

				CUMULATIVE_SCHEDULE_ACTIVATION = ACTIVATIONS_CLOCK_MASK;
				CUMULATIVE_SCHEDULE_ACTIVATION |= this->_d1.stepMask;
				CUMULATIVE_SCHEDULE_ACTIVATION |= this->_d2.stepMask;
				_needInit = false;
			}

			CUMULATIVE_SCHEDULE_ACTIVATION |= ACTIVATIONS_CLOCK_MASK;

			//subtract earliest plan other plans		
			triggerPlan(_d1, earliestActivationTime);
			triggerPlan(_d2, earliestActivationTime);

			//schedule
			while ((byte)(SCHEDULE_START + 1) == SCHEDULE_END) {
				//wait until schedule buffer has empty space
				Steppers::startScheduler();
			}

			SCHEDULE_BUFFER[SCHEDULE_START] = 65535 - earliestActivationTime;
			SCHEDULE_ACTIVATIONS[(byte)(SCHEDULE_START + 1)] = CUMULATIVE_SCHEDULE_ACTIVATION;
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
private:
	// data for the first dimension
	PlanType _d1;

	//data for the second dimension
	PlanType _d2;

	//determine whether initialization is needed
	bool _needInit;
};







#endif