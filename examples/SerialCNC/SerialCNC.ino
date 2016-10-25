#include "StepperControl.h"

// how many bytes contains instruction from controller
#define PLAN_SIZE 8 
#define INSTRUCTION_SIZE (1+PLAN_SIZE*4+2+1) 
#define BUFFERED_INSTRUCTION_COUNT 8
#define STEPPER_COUNT 2

byte STEP_CLK_PIN1 = 8;
byte STEP_DIR_PIN1 = 9;

byte STEP_CLK_PIN2 = 10;
byte STEP_DIR_PIN2 = 11;

//Buffer used in form of INSTRUCTION_SIZE segments which are filled with Serial data.
byte INSTRUCTION_BUFFER[INSTRUCTION_SIZE*BUFFERED_INSTRUCTION_COUNT] = { 0 };

//Index where next instruction segment will contain plan instruction.
byte INSTRUCTION_BUFFER_LAST_INDEX = 0;
//Index to a segment where data are actually written.
byte INSTRUCTION_BUFFER_ARRIVAL_INDEX = 0;
//Absolute offset to actually written segment.
byte INSTRUCTION_BUFFER_ARRIVAL_OFFSET = 0;
//Offset within actually written instruction segment.
byte SEGMENT_ARRIVAL_OFFSET = 0;

//Time where last byte has arrived (is used for incomplete message recoveries).
unsigned long LAST_BYTE_ARRIVAL_TIME = 0;

int32_t lastActivationSlack1 = 0;
int32_t lastActivationSlack2 = 0;

bool enableConstantSchedule = false;
PlanScheduler2D<ConstantPlan> CONSTANT_SCHEDULER(STEP_CLK_PIN1, STEP_DIR_PIN1, STEP_CLK_PIN2, STEP_DIR_PIN2);

bool enableAccelerationSchedule = false;
PlanScheduler2D<AccelerationPlan> ACCELERATION_SCHEDULER(STEP_CLK_PIN1, STEP_DIR_PIN1, STEP_CLK_PIN2, STEP_DIR_PIN2);


void setup() {
	Serial.begin(128000);

	pinMode(13, OUTPUT);
	pinMode(STEP_CLK_PIN1, OUTPUT);
	pinMode(STEP_DIR_PIN1, OUTPUT);
	pinMode(STEP_CLK_PIN2, OUTPUT);
	pinMode(STEP_DIR_PIN2, OUTPUT);

	digitalWrite(STEP_CLK_PIN1, HIGH);
	digitalWrite(STEP_CLK_PIN2, HIGH);

	Steppers::initialize();

	delay(1000);
	melodyStart();
}

void loop() {
	for (;;) {
		if (!enableAccelerationSchedule && !enableConstantSchedule)
			tryToFetchNextPlans();

		bool isPlanFinished = false;
		if (enableConstantSchedule)
			isPlanFinished = !CONSTANT_SCHEDULER.fillSchedule();
		else if (enableAccelerationSchedule)
			isPlanFinished = !ACCELERATION_SCHEDULER.fillSchedule();

		if (isPlanFinished) {
			// executed plans were finished
			if (enableConstantSchedule) {
				lastActivationSlack1 = CONSTANT_SCHEDULER.lastActivationSlack1;
				lastActivationSlack2 = CONSTANT_SCHEDULER.lastActivationSlack2;
				enableConstantSchedule = false;
			}

			if (enableAccelerationSchedule) {
				lastActivationSlack1 = ACCELERATION_SCHEDULER.lastActivationSlack1;
				lastActivationSlack2 = ACCELERATION_SCHEDULER.lastActivationSlack2;
				enableAccelerationSchedule = false;
			}

			Serial.print('F');
			continue;
		}

		//serial communication handling
		if (SEGMENT_ARRIVAL_OFFSET == INSTRUCTION_SIZE) {
			//we received an instrution - do processing
			processControllerInstruction();
		}
		else if (Serial.available()) {
			//new data byte arrived
			INSTRUCTION_BUFFER[INSTRUCTION_BUFFER_ARRIVAL_OFFSET + SEGMENT_ARRIVAL_OFFSET] = (byte)Serial.read();
			SEGMENT_ARRIVAL_OFFSET = boundedIncrement(SEGMENT_ARRIVAL_OFFSET, INSTRUCTION_SIZE + 1);
			LAST_BYTE_ARRIVAL_TIME = millis();
		}
		else if (SEGMENT_ARRIVAL_OFFSET > 0 && (millis() - LAST_BYTE_ARRIVAL_TIME) > 2) {
			//there may be some incomplete message - we cant wait more
			SEGMENT_ARRIVAL_OFFSET = 0;
			Serial.print('E'); //incomplete message erased			
		}
	}
}

// Processes buffer with instruction from controller - return false on invalid instruction
inline bool processControllerInstruction() {
	const byte* buffer = INSTRUCTION_BUFFER + INSTRUCTION_BUFFER_ARRIVAL_OFFSET;
	SEGMENT_ARRIVAL_OFFSET = 0;

	uint16_t checksum = 0;
	for (int i = 0; i < INSTRUCTION_SIZE - 2; ++i) {
		checksum += buffer[i];
	}

	uint16_t receivedChecksum = READ_UINT16(buffer, INSTRUCTION_SIZE - 2);
	if (receivedChecksum != checksum) {
		//we received invalid instruction
		Serial.print('C'); //invalid checksum
		return false;
	}

	//parse the instruction
	byte command = buffer[0];
	switch (command)
	{
	case 'A': //acceleration plan arrived
	case 'C': //constant plan arrived		
		if (!canAddPlan()) {
			//there is no more space for keeping the plan
			sendPlanOverflow();
			return false;
		}

		sendPlanAccepted();
		//shift the arrival index to the next one		
		INSTRUCTION_BUFFER_ARRIVAL_INDEX = boundedIncrement(INSTRUCTION_BUFFER_ARRIVAL_INDEX, BUFFERED_INSTRUCTION_COUNT);
		INSTRUCTION_BUFFER_ARRIVAL_OFFSET = INSTRUCTION_BUFFER_ARRIVAL_INDEX * INSTRUCTION_SIZE;
		return true;
	case 'I':
		//welcome message
		melody();
		Serial.print('I');
		return true;
	}

	//unknown command
	return false;
}

bool canAddPlan() {
	//we have to keep at least one instruction segment free for interactive instructions
	return boundedIncrement(INSTRUCTION_BUFFER_ARRIVAL_INDEX, BUFFERED_INSTRUCTION_COUNT) != INSTRUCTION_BUFFER_LAST_INDEX;
}

void sendPlanOverflow() {
	Serial.print('O');
}

void sendPlanAccepted() {
	Serial.print('Y');
}

void tryToFetchNextPlans() {
	if (INSTRUCTION_BUFFER_LAST_INDEX == INSTRUCTION_BUFFER_ARRIVAL_INDEX)
		//no more plans available
		return;

	byte* buffer = 1 + INSTRUCTION_BUFFER + (INSTRUCTION_BUFFER_LAST_INDEX * INSTRUCTION_SIZE);
	INSTRUCTION_BUFFER_LAST_INDEX = boundedIncrement(INSTRUCTION_BUFFER_LAST_INDEX, BUFFERED_INSTRUCTION_COUNT);

	switch (buffer[-1]) {
	case 'A': {
		enableAccelerationSchedule = true;
		ACCELERATION_SCHEDULER.registerLastActivationSlack(lastActivationSlack1, lastActivationSlack2);
		ACCELERATION_SCHEDULER.initFrom(buffer);
		break;
	}
	case 'C': {
		enableConstantSchedule = true;
		CONSTANT_SCHEDULER.registerLastActivationSlack(lastActivationSlack1, lastActivationSlack2);
		CONSTANT_SCHEDULER.initFrom(buffer);
		break;
	}
	default:
		//This should never happend - continuation would cause undefined behaviour
		//so we rather block here.
		for (;;)Serial.print('U');
		break;
	}
}

inline byte boundedIncrement(const byte valueToIncrement, const byte exclusiveBoundary) {
	if (valueToIncrement + 1 >= exclusiveBoundary)
		return 0;

	return valueToIncrement + 1;
}


void melody() {
	note(300, 800);
	note(100, 400);
	note(300, 600);
}

void melodyStart() {
	note(200, 600);
	note(200, 500);
	note(200, 400);
	note(200, 300);
	note(200, 200);
}

void note(int32_t length, int32_t tone) {
	for (int32_t i = 0; i < length * 1000; i += 2 * tone) {
		digitalWrite(STEP_DIR_PIN1, LOW);
		delayMicroseconds(10);
		digitalWrite(STEP_CLK_PIN1, LOW);
		delayMicroseconds(tone - 20);
		digitalWrite(STEP_CLK_PIN1, HIGH);
		delayMicroseconds(10);

		digitalWrite(STEP_DIR_PIN1, HIGH);
		delayMicroseconds(10);
		digitalWrite(STEP_CLK_PIN1, LOW);
		delayMicroseconds(tone - 20);
		digitalWrite(STEP_CLK_PIN1, HIGH);
		delayMicroseconds(10);
	}
}
