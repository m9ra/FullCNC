#include "StepperControl.h"

PlanScheduler4D<ConstantPlan> CONSTANT_SCHEDULER(SLOT1_CLK_MASK, SLOT1_DIR_MASK, SLOT0_CLK_MASK, SLOT0_DIR_MASK, SLOT3_CLK_MASK, SLOT3_DIR_MASK, SLOT2_CLK_MASK, SLOT2_DIR_MASK);

// the setup function runs once when you press reset or power the board
void setup() {
	Serial.begin(128000);

	pinMode(SLOT0_CLK_PIN, OUTPUT);
	pinMode(SLOT0_DIR_PIN, OUTPUT);

	pinMode(SLOT1_CLK_PIN, OUTPUT);
	pinMode(SLOT1_DIR_PIN, OUTPUT);

	pinMode(SLOT2_CLK_PIN, OUTPUT);
	pinMode(SLOT2_DIR_PIN, OUTPUT);

	pinMode(SLOT3_CLK_PIN, OUTPUT);
	pinMode(SLOT3_DIR_PIN, OUTPUT);

	Steppers::initialize();
}

void printTime(String message, unsigned long duration) {
	Serial.print(message);
	Serial.print(1.0*(duration));
	Serial.println("us");
}

void printTimeStep(String message, unsigned long duration, int32_t steps) {
	Serial.print(message);
	Serial.print(1.0*duration/steps);
	Serial.println("us");
}

void testActivationClock() {
	int16_t stepCount = 30000;
	int32_t stepDelay = 100;
	int32_t stepDelayTick = stepDelay * 2;
	int32_t measuringOverhead = 16; //us

	int32_t expectedMicroseconds = stepCount*stepDelay;

	byte data[64] = { 0 };
	byte plan1[] = { INT16_TO_BYTES(stepCount),INT32_TO_BYTES(stepDelayTick) };

	for (int i = 0; i < sizeof(plan1); ++i) {
		data[i] = plan1[i];
	}

	CONSTANT_SCHEDULER.initFrom(data);

	//fill schedule without scheduler enabling
	CONSTANT_SCHEDULER.fillSchedule(false);
	volatile unsigned long startTime = micros();
	while (CONSTANT_SCHEDULER.fillSchedule(true));
	while (TIMSK1 != 0);
	volatile unsigned long endTime = micros();


	int32_t duration = endTime - startTime - measuringOverhead;
	printTime("Expected duration: ", expectedMicroseconds);
	printTime("Measured duration: ", duration);
	printTimeStep("Duration difference per step: ", abs(expectedMicroseconds - duration), stepCount);
}


void loop() {
	testActivationClock();

	Serial.println();
	Serial.println();
	delay(1000);
}


