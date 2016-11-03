#include "StepperControl.h"

// how many bytes contains instruction from controller

#define INSTRUCTION_SIZE 59
#define BUFFERED_INSTRUCTION_COUNT 8
#define STEPPER_COUNT 2

byte STEP_CLK_PIN1 = 10;
byte STEP_DIR_PIN1 = 11;

byte STEP_CLK_PIN2 = 8;
byte STEP_DIR_PIN2 = 9;

//Buffer used in form of INSTRUCTION_SIZE segments which are filled with Serial data.
byte INSTRUCTION_BUFFER[INSTRUCTION_SIZE*BUFFERED_INSTRUCTION_COUNT] = { 0 };

//Index where next instruction segment will contain plan instruction.
uint16_t INSTRUCTION_BUFFER_LAST_INDEX = 0;
//Index to a segment where data are actually written.
uint16_t INSTRUCTION_BUFFER_ARRIVAL_INDEX = 0;
//Absolute offset to actually written segment.
uint16_t INSTRUCTION_BUFFER_ARRIVAL_OFFSET = 0;
//Offset within actually written instruction segment.
uint16_t SEGMENT_ARRIVAL_OFFSET = 0;

//Time where last byte has arrived (is used for incomplete message recoveries).
unsigned long LAST_BYTE_ARRIVAL_TIME = 0;

int32_t lastActivationSlack1 = 0;
int32_t lastActivationSlack2 = 0;

bool enableConstantSchedule = false;
PlanScheduler2D<ConstantPlan> CONSTANT_SCHEDULER(STEP_CLK_PIN1, STEP_DIR_PIN1, STEP_CLK_PIN2, STEP_DIR_PIN2);

bool enableAccelerationSchedule = false;
PlanScheduler2D<AccelerationPlan> ACCELERATION_SCHEDULER(STEP_CLK_PIN1, STEP_DIR_PIN1, STEP_CLK_PIN2, STEP_DIR_PIN2);


//homing interrupt
volatile byte HOME_MASK = 0;
ISR(PCINT1_vect) {
	setHomeMask();
}

void setup() {
	Serial.begin(128000);

	// initialize outputs
	pinMode(13, OUTPUT);
	pinMode(STEP_CLK_PIN1, OUTPUT);
	pinMode(STEP_DIR_PIN1, OUTPUT);
	pinMode(STEP_CLK_PIN2, OUTPUT);
	pinMode(STEP_DIR_PIN2, OUTPUT);

	digitalWrite(STEP_CLK_PIN1, HIGH);
	digitalWrite(STEP_CLK_PIN2, HIGH);

	//initialze inputs
	pinMode(A0, OUTPUT);
	pinMode(A3, OUTPUT);
	pinMode(A4, OUTPUT);
	pinMode(A5, OUTPUT);
	pinMode(A1, INPUT);
	pinMode(A2, INPUT);

	digitalWrite(A1, HIGH);
	digitalWrite(A2, HIGH);
	pciSetup(A1);
	pciSetup(A2);

	//initialize libraries
	Steppers::initialize();
	setHomeMask();
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
	case 'H':
		//homing procedure
		homing();
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

void homing() {
	if (enableAccelerationSchedule || enableAccelerationSchedule || Steppers::isSchedulerRunning()) {
		//cannot do homing because something is scheduled
		Serial.print('Q');
		return;
	}

	//TODO refactor pin numbers

	ACCELERATION_SCHEDULER.initForHoming();
	while (ACCELERATION_SCHEDULER.fillSchedule());
	while (HOME_MASK != 1 + 4)
	{
		CONSTANT_SCHEDULER.initForHoming();
		//we can't keep here some unplanned steps - flush them all
		while (CONSTANT_SCHEDULER.fillSchedule());
	}
	//arrived home

	// wait until all steps are flushed
	while (Steppers::isSchedulerRunning());
	
	//now go slowly back to release home switches
	digitalWrite(STEP_DIR_PIN1, LOW);
	digitalWrite(STEP_DIR_PIN2, LOW);
	delayMicroseconds(PORT_CHANGE_DELAY);
	while (HOME_MASK > 0)
	{
		if (HOME_MASK & 4)
			digitalWrite(STEP_CLK_PIN1, LOW);

		if (HOME_MASK & 1)
			digitalWrite(STEP_CLK_PIN2, LOW);

		delayMicroseconds(PORT_CHANGE_DELAY);
		digitalWrite(STEP_CLK_PIN1, HIGH);
		digitalWrite(STEP_CLK_PIN2, HIGH);

		delayMicroseconds(2000 - PORT_CHANGE_DELAY);
	}
	//homing was successful
	Serial.print('H');
}

void pciSetup(byte pin)
{
	*digitalPinToPCMSK(pin) |= bit(digitalPinToPCMSKbit(pin));  // enable pin
	PCIFR |= bit(digitalPinToPCICRbit(pin)); // clear any outstanding interrupt
	PCICR |= bit(digitalPinToPCICRbit(pin)); // enable interrupt for the group
}

inline void setHomeMask() {
	byte l1Pushed = (digitalRead(A1) == LOW) << 2;
	byte l2Pushed = (digitalRead(A2) == LOW) << 0;
	HOME_MASK = l1Pushed | l2Pushed;
	Steppers::setActivationMask(HOME_MASK);
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
