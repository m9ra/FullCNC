#include "StepperControl.h"

int STEP_CLK_PIN = 8;
int STEP_DIR_PIN = 9;
StepperGroup group1 = StepperGroup(1, new byte[1]{ STEP_CLK_PIN }, new byte[1]{ STEP_DIR_PIN });

// the setup function runs once when you press reset or power the board
void setup() {
	Serial.begin(128000);
	pinMode(STEP_CLK_PIN, OUTPUT);
	pinMode(STEP_DIR_PIN, OUTPUT);

	Steppers::initialize();
}

void printTime(String message, unsigned long duration, int16_t stepCount) {
	Serial.print(message);
	Serial.print(1.0*(duration) / stepCount);
	Serial.println("us");
}

class AccelerationPlanBenchmark : AccelerationPlan {
public:
	AccelerationPlanBenchmark() : AccelerationPlan(0, 0, 0) {}

	void run() {
		unsigned long duration;
		int16_t stepCount;

		stepCount = 1000;
		duration = this->_runPlan(stepCount, 2000, 100);
		printTime("AccelerationPlan Forward - 1000|2000|100: ", duration, stepCount);

		duration = this->_runPlan(-stepCount, 2000, 100);
		printTime("AccelerationPlan Backward - 1000|2000|100: ", duration, stepCount);

		stepCount = 5000;
		duration = this->_runPlan(stepCount, 2000, 100);
		printTime("AccelerationPlan Forward - 5000|2000|100: ", duration, stepCount);

		duration = this->_runPlan(-stepCount, 2000, 100);
		printTime("AccelerationPlan Backward - 5000|2000|100: ", duration, stepCount);

		stepCount = 5000;
		duration = this->_runPlan(stepCount, 20000, 100);
		printTime("AccelerationPlan Forward - 5000|20000|100: ", duration, stepCount);

		duration = this->_runPlan(-stepCount, 20000, 100);
		printTime("AccelerationPlan Backward - 5000|20000|100: ", duration, stepCount);

		stepCount = 5000;
		duration = this->_runPlan(stepCount, 20000, 10);
		printTime("AccelerationPlan Forward - 5000|20000|10: ", duration, stepCount);

		duration = this->_runPlan(-stepCount, 20000, 10);
		printTime("AccelerationPlan Backward - 5000|20000|10: ", duration, stepCount);
		Serial.println();
	}

private:
	unsigned long _runPlan(int16_t stepCount, uint16_t initialDeltaT, int16_t n) {
		this->_clockMask = 0;
		this->_dirMask = 0;
		this->_remainingSteps = abs(stepCount);
		(byte&)this->stepDirection = stepCount > 0 ? 1 : 0;
		this->_current2N = abs(2 * n);
		this->_isDeceleration = n < 0;
		this->_currentDeltaT = initialDeltaT;
		this->_currentDeltaTBuffer = 0;


		this->_isDirReported = false;
		this->_isActive = true;
		this->_isPulseStartPhase = true;

		unsigned long startTime = micros();
		while (this->_isActive) {
			this->_createNextActivation();
		}
		unsigned long endTime = micros();

		return endTime - startTime;
	}
};

class ConstantPlanBenchmark :ConstantPlan {
public:
	ConstantPlanBenchmark() :ConstantPlan(0, 0, 0, 0) {}

	void run() {
		unsigned long duration;
		int16_t stepCount = 10000;

		duration = this->_runPlan(stepCount);
		printTime("ConstantPlan Forward - no period: ", duration, stepCount);

		duration = this->_runPlan(-stepCount);
		printTime("ConstantPlan Backward - no period: ", duration, stepCount);

		duration = this->_runPlan(stepCount, 1, 2);
		printTime("ConstantPlan Forward - 1/2 period: ", duration, stepCount);

		duration = this->_runPlan(-stepCount, 1, 2);
		printTime("ConstantPlan Backward - 1/2 no period: ", duration, stepCount);

		duration = this->_runPlan(stepCount, 1, 200);
		printTime("ConstantPlan Forward - 1/200 period: ", duration, stepCount);

		duration = this->_runPlan(-stepCount, 1, 200);
		printTime("ConstantPlan Backward - 1/200 no period: ", duration, stepCount);

		Serial.println();
	}
private:
	unsigned long _runPlan(int16_t stepCount, uint16_t periodNumerator = 0, uint16_t periodDenominator = 0) {
		this->_clockMask = 0;
		this->_dirMask = 0;
		this->_remainingSteps = abs(stepCount);
		(byte&)this->stepDirection = stepCount > 0 ? 1 : 0;
		this->_periodAccumulator = 0;
		(uint16_t&)this->_periodNumerator = periodNumerator;
		(uint16_t&)this->_periodDenominator = periodDenominator;
		this->_isDirReported = false;
		this->_isActive = true;
		this->_isPulseStartPhase = true;

		unsigned long startTime = micros();
		while (this->_isActive) {
			this->_createNextActivation();
		}
		unsigned long endTime = micros();

		return endTime - startTime;
	}
};

void loop() {
	ConstantPlanBenchmark benchmark1 = ConstantPlanBenchmark();
	benchmark1.run();

	AccelerationPlanBenchmark benchmark2 = AccelerationPlanBenchmark();
	benchmark2.run();

	delay(1000);
}


/*
RESULTS_1
ConstantPlan Forward - no period: 10.19us
ConstantPlan Backward - no period: 9.83us
ConstantPlan Forward - 1/2 period: 12.57us
ConstantPlan Backward - 1/2 no period: 12.19us
ConstantPlan Forward - 1/200 period: 12.17us
ConstantPlan Backward - 1/200 no period: 11.79us

AccelerationPlan Forward - 1000|2000|100: 18.71us
AccelerationPlan Backward - 1000|2000|100: 18.32us
AccelerationPlan Forward - 5000|2000|100: 16.54us
AccelerationPlan Backward - 5000|2000|100: 16.16us
AccelerationPlan Forward - 5000|20000|100: 22.18us
AccelerationPlan Backward - 5000|20000|100: 21.81us
AccelerationPlan Forward - 5000|20000|10: 22.89us
AccelerationPlan Backward - 5000|20000|10: 22.51us


*/