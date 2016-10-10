#include "StepperControl.h"

int STEP_CLK_PIN = 8;
int STEP_DIR_PIN = 9;
StepperGroup group1 = StepperGroup(1, new byte[1]{ STEP_CLK_PIN }, new byte[1]{ STEP_DIR_PIN });

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



	Plan** plans;
	plans = new Plan*[1]{
		new AccelerationPlan(625, 1, 1, START_DELTA_T)
	};
	Steppers::runPlanning(group1, plans);

	plans = new Plan*[1]{
		new ConstantPlan(400 * 30 - 625 - 6250,100,0)
	};
	Steppers::runPlanning(group1, plans);

	plans = new Plan*[1]{
		new AccelerationPlan(6250, -1, 10, 100)
	};
	Steppers::runPlanning(group1, plans);

	Serial.println();
	digitalWrite(13, LOW);
	delay(1000);
}
