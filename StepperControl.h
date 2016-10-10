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
class Stepper;

class Fraction32 {
public:
	const int32_t Numerator;

	const int32_t Denominator;

	Fraction32(int32_t numerator, int32_t denominator = 1);

	int32_t to_int32_t();

	Fraction32 absolute();

	Fraction32 operator*(uint32_t factor);

	Fraction32 operator+(Fraction32 op2);

	Fraction32 operator-(Fraction32 op2);

	Fraction32 operator*(Fraction32 op2);

	Fraction32 operator/(Fraction32 op2);
};

class Plan {
	friend Steppers;
public:

	// Direction of all consequent steps of the plan.
	const byte stepDirection;

	Plan(int32_t stepCount);

private:
	// Outputs next step time (in 0.5us resolution) of the plan - zero means end of the plan.
	virtual uint16_t _createNextDeltaT() = 0;
};

class AccelerationPlan : public Plan {
public:
	AccelerationPlan(Fraction32& accelerationCoefficient, uint16_t initialDeltaT, int32_t stepCount);

private:
	// fraction of max acceleration according to precomputed acc. curve
	const Fraction32 _accelerationCoefficient;

	const int _accelerationIncrement;

	int32_t _doneTrack;

	// current deltaT which is used
	uint16_t _currentDeltaT;

	// how many steps remains to do with this plan
	int32_t _remainingSteps;

	virtual uint16_t _createNextDeltaT();
};

class ConstantPlan : public Plan {
public:
	ConstantPlan(int32_t stepCount, int32_t totalTime);

private:
	//how many steps remains to do with this plan
	long _remainingSteps;

	const uint16_t _baseDeltaT;

	const int32_t _remainderPeriod;

	int32_t _currentRemainderOffset;

	virtual uint16_t _createNextDeltaT();
};

class StepPlan {
	friend Stepper;

public:
	// How many steps should be done.
	const int count;

	// Delay between steps.
	const int delay;

	StepPlan(int count, int delay);

private:
	// Next plan in the linked list.
	StepPlan* _next_plan;
};

class Stepper {
	friend Steppers;

public:
	Stepper(int pinClk, int pinDir, int inertia);

	// Adds plan for the stepper for given counts (sign determines direction) and delay is a desired number of skipped control pulses between steps.
	void step(int count, int delay = 0);

	// Determine whether stepper has some plan to execute.
	bool isBusy();

private:
	// Initialize steppers pins.
	void _initialize();

	// Control pulse for the stepper 16khz.
	void _controlPulse(bool activePeriod);

	// Inertia settings of the stepper.
	const int _inertia;

	// Clock pin of the stepper.
	const int _pin_clk;

	// Dir pin of the stepper.
	const int _pin_dir;

	// How many iterations was done since last step.
	int _last_step_iterations;

	// How many steps in current plan is missing.
	int _planProgress;

	// What delay are we missing.
	int _delayProgress;

	// Next plan for the stepper.
	StepPlan* _next_plan;

	// Next plan for the stepper.
	StepPlan* _last_plan;

	// Currently processed plan.
	StepPlan* _active_plan;

	// Next stepper in linked list.
	Stepper* _next_stepper;
};

class Steppers {

public:
	// Initialize registered steppers - no new steppers can be created afterewards.
	static void initialize();

	// Routine that runs given plan on the stepper.
	static void runPlanning(Stepper& stepper, Plan& plan);

	static int32_t calculateAccelerationDistance(Fraction32& startVelocity, Fraction32& accelerationCoefficient, Fraction32& targetVelocity);

	// Creates a stepper using given digital pins and interna inertia.
	static Stepper & createStepper(int pinClk, int pinDir, int inertia);

private:
	// Determine whether all routines are initialized.
	static bool _is_initialized;

	// Determine whether current period is active.
	static bool _active_period;

	// First stepper in the linked list.
	static Stepper* _first_stepper;
};


#endif