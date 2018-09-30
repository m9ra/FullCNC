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


#define READ_INT16(buff, position) ((((int16_t)buff[(position)]) << 8) + buff[(position) + 1])
#define READ_INT32(buff, position) ((((int32_t)buff[(position)]) << 24)+(((int32_t)buff[(position) + 1]) << 16)+(((int32_t)buff[(position) + 2]) << 8) + buff[(position) + 3])
#define READ_UINT16(buff, position) ((((uint16_t)buff[(position)]) << 8) + buff[(position) + 1])
#define INT32_TO_BYTES(vl) (((byte*)&vl)[3]), (((byte*)&vl)[2]), (((byte*)&vl)[1]), (((byte*)&vl)[0])
#define INT16_TO_BYTES(vl) (((byte*)&vl)[1]), (((byte*)&vl)[0])
#define UINT16_MAX 65535
#define INT32_MAX 2147483647
#define INT32_MIN -2147483648


#define MAX_ACCELERATION 200 //rev/s^2
#define START_DELTA_T 350 //us
#define MIN_DELTA_T 100	//us
#define STEPS_PER_REVOLUTION 400 //200 with 1/2 microstep
#define TIMESCALE 1000000 //us
#define CLIP_D(delta) max(MIN_DELTA_T,min(START_DELTA_T,delta))

//port 8 (PORTB 1st bit)
#define SLOT0_CLK_PIN 8
#define SLOT0_CLK_MASK (1<<0)
//port 9 (PORTB 2nd bit)
#define SLOT0_DIR_PIN 9
#define SLOT0_DIR_MASK (1<<1)

//port 10 (PORTB 3rd bit)
#define SLOT1_CLK_PIN 10
#define SLOT1_CLK_MASK (1<<2)
//port 11 (PORTB 4th bit)
#define SLOT1_DIR_PIN 11
#define SLOT1_DIR_MASK (1<<3)

//port 4 (PORTD 5rd bit)
#define SLOT2_CLK_PIN 4
#define SLOT2_CLK_MASK (1<<4)
//port 5 (PORTD 6th bit)
#define SLOT2_DIR_PIN 5
#define SLOT2_DIR_MASK (1<<5)

//port 6 (PORTB 7th bit)
#define SLOT3_CLK_PIN 6
#define SLOT3_CLK_MASK (1<<6)
//port 7 (PORTB 8th bit)
#define SLOT3_DIR_PIN 7
#define SLOT3_DIR_MASK (1<<7)

#define B_SLOTS_MASK (SLOT0_DIR_MASK | SLOT0_CLK_MASK | SLOT1_DIR_MASK | SLOT1_CLK_MASK)
#define D_SLOTS_MASK (SLOT2_DIR_MASK | SLOT2_CLK_MASK | SLOT3_DIR_MASK | SLOT3_CLK_MASK)

// combined clock mask
#define ACTIVATIONS_CLOCK_MASK (SLOT0_CLK_MASK | SLOT1_CLK_MASK | SLOT2_CLK_MASK | SLOT3_CLK_MASK )

// how long (on 0.5us scale)
//	* before pulse the dir has to be specified 
// KEEPING BOTH VALUES SAME enables computation optimization
#define PORT_CHANGE_DELAY (20 * 2)

// minimal time between two activations (is used for activation grouping)
#define MIN_ACTIVATION_DELAY (10 * 2)

// compensetaion subtracted for every activation (has to be smaller than min activation delay)
#define TIMER_RESET_COMPENSATION 10

// length of the schedule buffer (CANNOT be changed easily - it counts on byte overflows)
#define SCHEDULE_BUFFER_LEN 256

// buffer where instruction ends are marked for schedule.
extern bool INSTRUCTION_ENDS[];
// buffer for step signal timing
extern uint16_t SCHEDULE_BUFFER[];
// bitwise activation mask for step signals (selecting active ports)
extern byte SCHEDULE_ACTIVATIONS[];

// distance from home in steps
extern volatile int32_t SLOT0_STEPS;
// distance from home in steps
extern volatile int32_t SLOT1_STEPS;
// distance from home in steps
extern volatile int32_t SLOT2_STEPS;
// distance from home in steps
extern volatile int32_t SLOT3_STEPS;

extern volatile byte FINISHED_INSTRUCTION_COUNT;

// pointer where new timing will be stored
extern volatile byte SCHEDULE_START;
// pointer where scheduler is actually reading
extern volatile byte SCHEDULE_END;
// cumulative activation with state up to lastly scheduled activation
extern byte CUMULATIVE_SCHEDULE_ACTIVATION;

//Determine whether scheduler was stopped from last flag reset (is useful for plan schedulers)
extern volatile bool SCHEDULER_STOP_EVENT_FLAG;

//Determine whether scheduler was started from last flag reset (is useful for plan schedulers)
extern volatile bool SCHEDULER_START_EVENT_FLAG;


class Plan {
public:
	// Time of next scheduled activation
	int32_t nextActivationTime;

	// How many steps was planned by this plan.
	uint16_t stepCount;

	// How many steps remains to do with this plan.
	uint16_t remainingSteps;

	// Determine whether plan is still active.
	bool isActive;

	// Determine whether this plan starts with zero activation slack
	bool isActivationBoundary;

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
	static const byte dataSize = 10;

	ConstantPlan(byte clkPin, byte dirPin);

	// Loads plan from given data.
	void loadFrom(byte* data);

	// Initialize plan for homing routine.
	void initForHoming();

	// Creates next activation.
	void createNextActivation();

private:
	// Base deltaT for step rate.
	int32_t _baseDeltaT;
	// Period numerator for delay remainder displacement.
	uint16_t _periodNumerator;
	//Period denominator for delay remainder displacement.
	uint16_t _periodDenominator;
	//Period accumulator for remainder displacement.
	uint32_t _periodAccumulator;

	// Determine whether offset is defined for the instruction
	bool _hasOffset;
	// The offset
	int32_t _offset;
};

class AccelerationPlan : public Plan {
public:
	// How much data is required for load.
	static const byte dataSize = 14;

	AccelerationPlan(byte clkPin, byte dirPin);

	// Loads plan from given data.
	void loadFrom(byte* data);

	// Initialize plan for homing routine.
	void initForHoming();

	// Creates next activation.
	void createNextActivation();
protected:
	// determine whether plan corresponds to deceleration
	bool _isDeceleration;
	// current n parameter of Taylor incremental acceleration formula
	uint32_t _current4N;
	// buffer for remainder accumulation
	uint32_t _currentDeltaTBuffer2;
	// current deltaT which is used
	int32_t _currentDeltaT;
	// base delta for each step
	int32_t _baseDeltaT;
	// remainder of base delta which will be distributed throughout all steps
	int16_t _baseRemainder;
	// buffer for base remainder distribution counting
	int32_t _baseRemainderBuffer;
};

class Steppers {
public:
	// Initialize registered steppers - no new steppers can be created afterewards.
	static void initialize();

	// Starts scheduler.
	static bool startScheduler();

	// Determine whether scheduler is started.
	static bool isSchedulerRunning();

	// Blocks given ports by mask (one blocks, zero unblocks)
	static void setActivationMask(byte mask);
private:
	// Determine whether steppers environment is initialized.
	static bool _isInitialized;
};


struct ActivationSlack4D
{
	int32_t d1;
	int32_t d2;
	int32_t d3;
	int32_t d4;

	inline void reset() {
		d1 = 0;
		d2 = 0;
		d3 = 0;
		d4 = 0;
	}
};

template<typename PlanType> class PlanScheduler4D {
public:
	ActivationSlack4D slack;

	PlanScheduler4D(byte clkMask1, byte dirMask1, byte clkMask2, byte dirMask2, byte clkMask3, byte dirMask3, byte clkMask4, byte dirMask4)
		:_d1(clkMask1, dirMask1), _d2(clkMask2, dirMask2), _d3(clkMask3, dirMask3), _d4(clkMask4, dirMask4), _needInit(false),_hasEnd(false), slack()
	{
		slack.reset();
	}

	void registerLastActivationSlack(ActivationSlack4D &slack) {
		this->slack = slack;
	}

	void initForHoming() {
		this->_d1.initForHoming();
		this->_d2.initForHoming();
		this->_d3.initForHoming();
		this->_d4.initForHoming();

		this->_d1.createNextActivation();
		this->_d2.createNextActivation();
		this->_d3.createNextActivation();
		this->_d4.createNextActivation();
		this->_needInit = true;
		this->_hasEnd = false;
	}

	// loads plan from given data
	void initFrom(byte * data)
	{
		if (SCHEDULER_STOP_EVENT_FLAG) {
			//scheduler reset means slack reset
			this->slack.reset();
			SCHEDULER_STOP_EVENT_FLAG = false;
		}


		this->_d1.loadFrom(data);
		this->_d2.loadFrom(data + _d1.dataSize);
		this->_d3.loadFrom(data + _d2.dataSize + _d1.dataSize);
		this->_d4.loadFrom(data + _d3.dataSize + _d2.dataSize + _d1.dataSize);

		this->_d1.createNextActivation();
		this->_d2.createNextActivation();
		this->_d3.createNextActivation();
		this->_d4.createNextActivation();

		bool isStepTimeMissed = false;
		
		isStepTimeMissed |= this->applySlack(this->slack.d1, _d1);
		isStepTimeMissed |= this->applySlack(this->slack.d2, _d2);
		isStepTimeMissed |= this->applySlack(this->slack.d3, _d3);
		isStepTimeMissed |= this->applySlack(this->slack.d4, _d4);

		if (isStepTimeMissed)
			Serial.print('M');

		this->_needInit = true;
		this->_hasEnd = true;
	}

	// fills schedule buffer with plan data
	// returns true when buffer is full (temporarly), false when plan is over
	bool fillSchedule(bool startScheduler = true) {
		while (_d1.isActive || _d2.isActive || _d3.isActive || _d4.isActive) {
			//find earliest plan
			int32_t minActiveActivationTime = INT32_MAX;
			//TODO this can be slightly optimized (binary tree like comparison)
			if (_d1.isActive)
				minActiveActivationTime = min(minActiveActivationTime, _d1.nextActivationTime);
			if (_d2.isActive)
				minActiveActivationTime = min(minActiveActivationTime, _d2.nextActivationTime);
			if (_d3.isActive)
				minActiveActivationTime = min(minActiveActivationTime, _d3.nextActivationTime);
			if (_d4.isActive)
				minActiveActivationTime = min(minActiveActivationTime, _d4.nextActivationTime);

			//limit activation to timer resolution (we can output empty activation intermediate step)
			uint16_t earliestActivationTime = min(UINT16_MAX, minActiveActivationTime);

			if (_needInit) {
				earliestActivationTime = PORT_CHANGE_DELAY;

				CUMULATIVE_SCHEDULE_ACTIVATION = ACTIVATIONS_CLOCK_MASK;
				CUMULATIVE_SCHEDULE_ACTIVATION |= this->_d1.stepMask;
				CUMULATIVE_SCHEDULE_ACTIVATION |= this->_d2.stepMask;
				CUMULATIVE_SCHEDULE_ACTIVATION |= this->_d3.stepMask;
				CUMULATIVE_SCHEDULE_ACTIVATION |= this->_d4.stepMask;
				_needInit = false;
			}

			CUMULATIVE_SCHEDULE_ACTIVATION |= ACTIVATIONS_CLOCK_MASK;

			//subtract earliest plan other plans		
			triggerPlan(_d1, earliestActivationTime);
			triggerPlan(_d2, earliestActivationTime);
			triggerPlan(_d3, earliestActivationTime);
			triggerPlan(_d4, earliestActivationTime);

			//schedule
			while ((byte)(SCHEDULE_START + 1) == SCHEDULE_END && startScheduler) {
				//wait until schedule buffer has empty space				
				Steppers::startScheduler();
			}

			SCHEDULE_BUFFER[SCHEDULE_START] = UINT16_MAX - earliestActivationTime + TIMER_RESET_COMPENSATION;
			INSTRUCTION_ENDS[SCHEDULE_START] = this->_hasEnd && !(_d1.isActive || _d2.isActive || _d3.isActive || _d4.isActive);
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
		this->slack.d1 = _d1.nextActivationTime;
		this->slack.d2 = _d2.nextActivationTime;
		this->slack.d3 = _d3.nextActivationTime;
		this->slack.d4 = _d4.nextActivationTime;
		
		if (startScheduler)
			Steppers::startScheduler();
		return false;
	}
private:

	inline bool applySlack(int32_t &slackTime, PlanType &plan) {
		if (plan.isActivationBoundary) {
			slackTime = 0;
			return false;
		}

		plan.nextActivationTime += slackTime;
		if (plan.nextActivationTime < PORT_CHANGE_DELAY) {
			plan.nextActivationTime = PORT_CHANGE_DELAY;

			//we cannot go backwards in time - step was missed
			return true;
		}

		//step was not missed
		return false;
	}

	inline void triggerPlan(PlanType& plan, uint16_t nextActivationTime) {
		if (!plan.isActive) {
			if (!plan.isActivationBoundary)
				// this plan is not a boundary - continue to calculate slack
				plan.nextActivationTime -= nextActivationTime;
			//there is nothing to do
			return;
		}

		plan.nextActivationTime -= nextActivationTime;

		if (plan.nextActivationTime > MIN_ACTIVATION_DELAY)
			//no steps for the plan now
			return;

		// make the appropriate pin LOW
		CUMULATIVE_SCHEDULE_ACTIVATION &= ~(plan.clkMask);

		if (plan.nextActivationTime > 0) {
			//activations would come too early one after another - we will group them
			byte skippedTime = plan.nextActivationTime;
			plan.createNextActivation();
			plan.nextActivationTime += skippedTime;
		}
		else {
			//compute next activation
			plan.createNextActivation();
		}
	}


	// data for the first dimension
	PlanType _d1;

	//data for the second dimension
	PlanType _d2;

	//data for the third dimension
	PlanType _d3;

	//data for the fourth dimension
	PlanType _d4;

	//determine whether initialization is needed
	bool _needInit;

	///determine whether instruction end will be reported to scheduler
	bool _hasEnd;
};

#endif