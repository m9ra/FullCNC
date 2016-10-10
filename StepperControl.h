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

#define MAX_ACCELERATION 200 //rev/s^2
#define START_DELTA_T 350 //us
#define MIN_DELTA_T 100	//us
#define STEPS_PER_REVOLUTION 400 //200 with 1/2 microstep
#define TIMESCALE 1000000 //us
#define CLIP_D(delta) max(MIN_DELTA_T,min(START_DELTA_T,delta))

class Steppers;
class StepperGroup;

class Plan {
	friend Steppers;
public:

	// Direction of all consequent steps of the plan.
	const byte stepDirection;

	Plan(int32_t stepCount);

	

private:
	// Determine whether plan is still active.
	// Is managed by the planner.
	bool _isActive;

	// Time of next schedule of the plan. 
	// Is managed by the planner.
	uint16_t _nextScheduleTime;

	// Outputs next step time (in 0.5us resolution) of the plan - zero means end of the plan.
	virtual uint16_t _createNextDeltaT() = 0;
};

class AccelerationPlan : public Plan {
public:
	AccelerationPlan(int16_t stepCount, int16_t accelerationNumerator, int16_t accelerationDenominator, uint16_t initialDelta);

private:
	// how many steps remains to do with this plan
	uint16_t _remainingSteps;

	// numerator used for acceleration factoring
	const uint16_t _accelerationNumerator;

	// denominator used for acceleration factoring
	const uint16_t _accelerationDenominator;

	// buffer for acceleration numerator buffering (to avoid multiplication)
	uint32_t _accelerationNumeratorBuffer;

	// determine whether plan corresponds to deceleration
	bool _isDeceleration;

	// current deltaT which is used
	uint16_t _currentDeltaT;

	virtual uint16_t _createNextDeltaT();
};

class ConstantPlan : public Plan {
public:
	ConstantPlan(int16_t stepCount, uint16_t baseDeltaT, uint16_t remainderPeriod);

private:
	//how many steps remains to do with this plan
	uint16_t _remainingSteps;

	const uint16_t _baseDeltaT;

	const uint16_t _remainderPeriod;

	int32_t _currentRemainderOffset;

	virtual uint16_t _createNextDeltaT();
};

class StepperGroup {
	friend Steppers;
public:
	//How many steppers is contained by the group
	const byte StepperCount;

	// Creates group with specified number of steppers.
	StepperGroup(byte stepperCount, byte clockPins[], byte dirPins[]);

private:

	//port masks for clock (only PORTB is supported)
	byte* _clockBports;

	//pinout masks for direction (only PORTB is supported)
	byte* _dirBports;
};

class Steppers {

public:
	// Initialize registered steppers - no new steppers can be created afterewards.
	static void initialize();

	// Routine that runs given plans on the group of steppers.
	// Plans and containing array are DESTROYED by this method.
	static void runPlanning(StepperGroup& group, Plan** plans);

	//---------MORE GRANULAR PLANNING CONTROL---------
	// The following API is exposed to do async work during planning
	//
	// initPlanning(...);  
	// while(fillSchedule(...));
	// delete [plans and every contained plan];
	//
	// is equivalent to runPlanning(...);

	// Has to be called before calling fillSchedulle()
	inline static void initPlanning(StepperGroup& group, Plan** plans);

	// Fills schedule buffer with given plans (and ensures scheduler is enabled along the way). Returns false if plans are complete.
	inline static bool fillSchedule(StepperGroup& group, Plan** plans);
private:
	// Determine whether all routines are initialized.
	static bool _is_initialized;
};


#endif