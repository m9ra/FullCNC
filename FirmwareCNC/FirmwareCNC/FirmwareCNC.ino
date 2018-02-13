#include "StepperControl.h"

// how many bytes contains instruction from controller

#define INSTRUCTION_SIZE 59
#define BUFFERED_INSTRUCTION_COUNT 6
#define STEPPER_COUNT 2

//Buffer used in form of INSTRUCTION_SIZE segments which are filled with Serial data.
byte INSTRUCTION_BUFFER[INSTRUCTION_SIZE*BUFFERED_INSTRUCTION_COUNT] = { 0 };

// determine whether home position was calibrated already
bool IS_HOME_CALIBRATED = false;

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

ActivationSlack4D lastSlack = { 0 };

bool enableConstantSchedule = false;
PlanScheduler4D<ConstantPlan> CONSTANT_SCHEDULER(SLOT1_CLK_MASK, SLOT1_DIR_MASK, SLOT0_CLK_MASK, SLOT0_DIR_MASK, SLOT3_CLK_MASK, SLOT3_DIR_MASK, SLOT2_CLK_MASK, SLOT2_DIR_MASK);

bool enableAccelerationSchedule = false;
PlanScheduler4D<AccelerationPlan> ACCELERATION_SCHEDULER(SLOT1_CLK_MASK, SLOT1_DIR_MASK, SLOT0_CLK_MASK, SLOT0_DIR_MASK, SLOT3_CLK_MASK, SLOT3_DIR_MASK, SLOT2_CLK_MASK, SLOT2_DIR_MASK);

//homing interrupt
volatile byte HOME_MASK = 0;
ISR(PCINT1_vect) {
	setHomeMask();
}

void setup() {
	Serial.begin(128000);

	// initialize outputs
	pinMode(13, OUTPUT);

	pinMode(SLOT0_CLK_PIN, OUTPUT);
	pinMode(SLOT0_DIR_PIN, OUTPUT);

	pinMode(SLOT1_CLK_PIN, OUTPUT);
	pinMode(SLOT1_DIR_PIN, OUTPUT);

	pinMode(SLOT2_CLK_PIN, OUTPUT);
	pinMode(SLOT2_DIR_PIN, OUTPUT);

	pinMode(SLOT3_CLK_PIN, OUTPUT);
	pinMode(SLOT3_DIR_PIN, OUTPUT);

	digitalWrite(SLOT0_CLK_PIN, HIGH);
	digitalWrite(SLOT1_CLK_PIN, HIGH);
	digitalWrite(SLOT2_CLK_PIN, HIGH);
	digitalWrite(SLOT3_CLK_PIN, HIGH);

	//initialze inputs
	pinMode(A0, OUTPUT);
	pinMode(A3, OUTPUT);
	pinMode(A4, OUTPUT);
	pinMode(A5, OUTPUT);
	pinMode(A1, INPUT);
	pinMode(A2, INPUT);

	digitalWrite(A1, HIGH);
	digitalWrite(A2, HIGH);
	digitalWrite(A3, HIGH);
	digitalWrite(A4, HIGH);
	pciSetup(A1);
	pciSetup(A2);
	pciSetup(A3);
	pciSetup(A4);

	//initialize libraries
	Steppers::initialize();
	setHomeMask();
	delay(1000);
	melodyStart();
}

void loop() {
	Serial.print('1'); // the device is ready

	//wait until machine authenticates - this prevenets stall instructions from beiing executed.
	waitForAuthentication();

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
				lastSlack = CONSTANT_SCHEDULER.slack;
				enableConstantSchedule = false;
			}

			if (enableAccelerationSchedule) {
				lastSlack = ACCELERATION_SCHEDULER.slack;
				enableAccelerationSchedule = false;
			}

			//Serial.print('F');  //report instruction steps are all scheduled
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
	case 'D': {
		//state data request
		byte data[] = {
			'D', IS_HOME_CALIBRATED,
			INT32_TO_BYTES(SLOT1_STEPS),
			INT32_TO_BYTES(SLOT0_STEPS),
			INT32_TO_BYTES(SLOT3_STEPS),
			INT32_TO_BYTES(SLOT2_STEPS)
		};

		Serial.write(data, sizeof(data));
		return true;
	}
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

	ACCELERATION_SCHEDULER.initForHoming();
	while (ACCELERATION_SCHEDULER.fillSchedule());
	while (HOME_MASK != ACTIVATIONS_CLOCK_MASK)
	{
		CONSTANT_SCHEDULER.initForHoming();
		//we can't keep here some unplanned steps - flush them all
		while (CONSTANT_SCHEDULER.fillSchedule());
	}
	//arrived home

	// wait until all steps are flushed
	while (Steppers::isSchedulerRunning());

	//now go slowly back to release home switches
	digitalWrite(SLOT0_DIR_PIN, LOW);
	digitalWrite(SLOT1_DIR_PIN, LOW);
	digitalWrite(SLOT2_DIR_PIN, LOW);
	digitalWrite(SLOT3_DIR_PIN, LOW);
	delayMicroseconds(PORT_CHANGE_DELAY);
	int slot0_home_rev = 0;
	int slot1_home_rev = 0;
	int slot2_home_rev = 0;
	int slot3_home_rev = 0;

	while (HOME_MASK > 0)
	{
		if (HOME_MASK & SLOT0_CLK_MASK) {
			digitalWrite(SLOT0_CLK_PIN, LOW);
			++slot0_home_rev;
		}

		if (HOME_MASK & SLOT1_CLK_MASK) {
			digitalWrite(SLOT1_CLK_PIN, LOW);
			++slot1_home_rev;
		}

		if (HOME_MASK & SLOT2_CLK_MASK) {
			digitalWrite(SLOT2_CLK_PIN, LOW);
			++slot2_home_rev;
		}

		if (HOME_MASK & SLOT3_CLK_MASK) {
			digitalWrite(SLOT3_CLK_PIN, LOW);
			++slot3_home_rev;
		}

		delayMicroseconds(PORT_CHANGE_DELAY);
		digitalWrite(SLOT0_CLK_PIN, HIGH);
		digitalWrite(SLOT1_CLK_PIN, HIGH);
		digitalWrite(SLOT2_CLK_PIN, HIGH);
		digitalWrite(SLOT3_CLK_PIN, HIGH);

		delayMicroseconds(4000 - PORT_CHANGE_DELAY);
	}

	//scheduler is disabled here - is it safe to udpate that without locking
	SLOT0_STEPS = 0;
	SLOT1_STEPS = 0;
	SLOT2_STEPS = 0;
	SLOT3_STEPS = 0;

	Serial.print('|');
	Serial.print(slot1_home_rev);
	Serial.print(',');
	Serial.print(slot0_home_rev);
	Serial.print(',');
	Serial.print(slot3_home_rev);
	Serial.print(',');
	Serial.println(slot2_home_rev);

	IS_HOME_CALIBRATED = true;
	//homing was successful
	Serial.print('H');
}

void waitForAuthentication() {
	int authenticationStep = 0;
	const char* password = "$%#";
	Serial.print("a");
	while (authenticationStep < strlen(password))
	{
		char b = Serial.read();
		if (b <= 0)
			continue;

		if (password[authenticationStep] == b) {
			++authenticationStep;
		}
		else
		{
			authenticationStep = 0;
			Serial.print("a");
		}
	}
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
	byte l3Pushed = (digitalRead(A3) == LOW) << 6;
	byte l4Pushed = (digitalRead(A4) == LOW) << 4;
	byte mask = l1Pushed | l2Pushed | l3Pushed | l4Pushed;
	HOME_MASK = mask;
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
		ACCELERATION_SCHEDULER.registerLastActivationSlack(lastSlack);
		ACCELERATION_SCHEDULER.initFrom(buffer);
		break;
	}
	case 'C': {
		enableConstantSchedule = true;
		CONSTANT_SCHEDULER.registerLastActivationSlack(lastSlack);
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
		digitalWrite(SLOT0_DIR_PIN, LOW);
		delayMicroseconds(10);
		digitalWrite(SLOT0_CLK_PIN, LOW);
		delayMicroseconds(tone - 20);
		digitalWrite(SLOT0_CLK_PIN, HIGH);
		delayMicroseconds(10);

		digitalWrite(SLOT0_DIR_PIN, HIGH);
		delayMicroseconds(10);
		digitalWrite(SLOT0_CLK_PIN, LOW);
		delayMicroseconds(tone - 20);
		digitalWrite(SLOT0_CLK_PIN, HIGH);
		delayMicroseconds(10);
	}
}
