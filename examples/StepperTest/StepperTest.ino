#include "StepperControl.h"

int STEP_CLK_PIN = 8;
int STEP_DIR_PIN = 9;
Stepper& stepper1 = Steppers::createStepper(8, 9, 110);

// the setup function runs once when you press reset or power the board
void setup() {
	Serial.begin(128000);
	// initialize digital pin 13 as an output.
	pinMode(13, OUTPUT);
	pinMode(STEP_CLK_PIN, OUTPUT);
	pinMode(STEP_DIR_PIN, OUTPUT);

	Steppers::initialize();
}


// the loop function runs over and over again forever
void loop() {
	digitalWrite(13, HIGH);

	int32_t totalSteps = 400 * 10;
	Fraction32 desiredSpeed = Fraction32(1500L*400, 60);
	Fraction32 accelerationCoeff = Fraction32(1, 1);
	Fraction32 decelerationCoeff = accelerationCoeff *-1;
	Fraction32 startVelocity = Fraction32(0);

	int16_t decelerationStartDeltaT = (uint16_t)(Fraction32(TIMESCALE) / desiredSpeed).to_int32_t();
	Serial.println(decelerationStartDeltaT);
	int32_t accelerationSteps = Steppers::calculateAccelerationDistance(startVelocity, accelerationCoeff, desiredSpeed);
	AccelerationPlan plan1 = AccelerationPlan(accelerationCoeff, START_DELTA_T, accelerationSteps);

	int32_t constantStepCount = totalSteps - 2 * accelerationSteps;
	Serial.println(constantStepCount);
	int32_t time = (Fraction32(constantStepCount) / desiredSpeed*TIMESCALE).to_int32_t();
	Serial.println(time);
	Serial.flush();
	ConstantPlan plan2 = ConstantPlan(constantStepCount, time);
	AccelerationPlan plan3 = AccelerationPlan(decelerationCoeff, decelerationStartDeltaT, accelerationSteps);

	Steppers::runPlanning(stepper1, plan1);
	Steppers::runPlanning(stepper1, plan2);
	Steppers::runPlanning(stepper1, plan3);

	while (stepper1.isBusy()) {
		delay(10);
	}

	digitalWrite(13, LOW);
	delay(1000);
}
