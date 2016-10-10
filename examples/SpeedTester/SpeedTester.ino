int STEP_CLK_PIN = 8;
int STEP_DIR_PIN = 9;

#define CLR(x,y) (x&=(~(1<<y)))

#define SET(x,y) (x|=(1<<y))

volatile long long REMAINING_STEPS = 0;
volatile long long DONE_STEPS = 0;
uint16_t currentDeltaT = 0;

int step = 0;
#define START_DELTA_T 400
uint16_t accelerationTable[START_DELTA_T + 1];

// the setup function runs once when you press reset or power the board
void setup() {
	// initialize digital pin 13 as an output.
	Serial.begin(9600);
	
	pinMode(13, OUTPUT);
	pinMode(STEP_CLK_PIN, OUTPUT);
	pinMode(STEP_DIR_PIN, OUTPUT);

	initializeAccelerationTable();

	noInterrupts(); // disable all interrupts
	TCCR1A = 0;
	TCCR1B = 0;

	//TCNT1 = 34286; // preload timer 65536-16MHz/256/2Hz
	TCCR1B |= 1 << CS11; // 8 prescaler
	interrupts(); // enable all interrupts
}

void doSteps(long stepCount, int direction, int speed = 30) {
	digitalWrite(STEP_CLK_PIN, LOW);
	digitalWrite(STEP_DIR_PIN, direction);
	int acceleration = 1;
	long accelerationStep = 0;
	long accelerationAccumulator = 0;
	int startSpeed = 300;
	int rampStart = startSpeed - speed;
	for (long i = 0; i < stepCount; ++i) {
		digitalWrite(STEP_CLK_PIN, HIGH);
		digitalWrite(STEP_CLK_PIN, LOW);

		if (accelerationAccumulator > accelerationStep) {
			accelerationAccumulator = 0;
			accelerationStep += 10;
			int maxAccelerationStep = 3000L;
			if (accelerationStep > maxAccelerationStep)
				accelerationStep = maxAccelerationStep;

			rampStart -= acceleration;
			if (rampStart < 0)
				rampStart = 0;
		}

		long rampEnd = 0;
		long ramp = max(rampStart, rampEnd);

		int delay = speed - 4 + ramp;
		delayMicroseconds(delay);
		accelerationAccumulator += delay;
	}
}

void initializeAccelerationTable() {
	long long stepsPerRevolution = 400;
	long long maxAcceleration = 400;
	long long minDeltaT = 30;
	long long timeScale = 1000000;
	for (int deltaT = START_DELTA_T; deltaT >= minDeltaT; --deltaT) {
		/*long v = timeScale / (deltaT * stepsPerRevolution);
		long t = v / maxAcceleration;
		long s = maxAcceleration*t*t * stepsPerRevolution / 2;*/
		long s = maxAcceleration*timeScale*timeScale / maxAcceleration / maxAcceleration / stepsPerRevolution / deltaT / deltaT / 2;
		Serial.print(deltaT);
		Serial.print(" ");
		Serial.println(s);

		accelerationTable[deltaT] = s;
	}

	accelerationTable[0] = minDeltaT;
}


ISR(TIMER1_OVF_vect) {
	TCNT1 = 65535 - currentDeltaT * 2;
	if (REMAINING_STEPS <= 0) {
		TIMSK1 = 0;
		return;
	}

	SET(PORTB, 0);
	if (DONE_STEPS > REMAINING_STEPS) {

		if(currentDeltaT < START_DELTA_T && REMAINING_STEPS < accelerationTable[currentDeltaT])
		{
			++currentDeltaT;
		}
		//Serial.println(currentDeltaT);
	}
	else {
		/*
		long factor = sqrt(400L * 400L * 2L * DONE_STEPS);
		int currentDeltaT2 = 1000000L / factor;

		currentDeltaT2 = max(40, min(300, currentDeltaT2));
		*/

		if(currentDeltaT > accelerationTable[0] && DONE_STEPS > accelerationTable[currentDeltaT])
		{
			--currentDeltaT;
		}
	}


	REMAINING_STEPS -= 1;
	DONE_STEPS += 1;
	CLR(PORTB, 0);
}

void doSteps2(long stepCount, int direction, int speed = 65) {
	digitalWrite(STEP_CLK_PIN, LOW);
	digitalWrite(STEP_DIR_PIN, direction);
	currentDeltaT = START_DELTA_T;

	REMAINING_STEPS = stepCount;
	DONE_STEPS = 0;

	TCNT1 = 65535 - 2 * currentDeltaT;
	TIMSK1 = (1 << TOIE1);

	while (REMAINING_STEPS > 0)
		delay(1);

}

volatile int testVar = 0;

void callTest() {
	testVar += 1;
}

// the loop function runs over and over again forever
void loop() {
	/*
	int maxCount = 10000;
	long start = micros();
	for (volatile int i = 0; i < maxCount; ++i) {
	}
	long end = micros();

	long start2 = micros();
	for (volatile int i = 0; i < maxCount; ++i) {
		//testVar += 1;
		_NOP();
		callTest();
	}
	long end2 = micros();

	Serial.println(((end2 - start2) - (end - start)) * 1000 / maxCount);
	return;
	*/

		
	Serial.println("Begining");
	step += 1;
	doSteps2(400L * 300, 1);
	delay(10);
	doSteps2(400L * 10, 0);
	//doSteps(400 * 2, 0);
	delay(1000);
}
