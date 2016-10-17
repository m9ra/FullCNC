#include "StepperControl.h"

// how many bytes contains instruction from controller
#define INSTRUCTION_SIZE 36 
#define PLANS_BUFFER_SIZE 8 

#define READ_INT16(buff, position) (((int16_t)buff[position]) << 8) + buff[position + 1]
#define READ_UINT16(buff, position) (((uint16_t)buff[position]) << 8) + buff[position + 1]

int STEP_CLK_PIN = 8;
int STEP_DIR_PIN = 9;


Plan** PLANS_BUFFER[PLANS_BUFFER_SIZE] = { NULL }; //here buffered plans will be stored
byte ARRIVAL_BUFFER[INSTRUCTION_SIZE] = { NULL };
byte NEXT_PLAN_INDEX = 0; //index to next plan that will be scheduled
byte ARRIVAL_PLAN_INDEX = 0; //index where new plan can arrive
byte ARRIVAL_BUFFER_INDEX = 0; //index to arrival buffer
unsigned long LAST_BYTE_ARRIVAL_TIME = 0;
Plan** EXECUTED_PLANS = NULL;

StepperGroup group1 = StepperGroup(1, new byte[1]{ STEP_CLK_PIN }, new byte[1]{ STEP_DIR_PIN });

void setup() {
	Serial.begin(128000);

	pinMode(13, OUTPUT);
	pinMode(STEP_CLK_PIN, OUTPUT);
	pinMode(STEP_DIR_PIN, OUTPUT);
	digitalWrite(STEP_CLK_PIN, HIGH);

	Steppers::initialize();

	delay(1000);
	melodyStart();
}

void loop() {
	digitalWrite(13, HIGH);
	//demo();
	//return;

	for (;;) {
		if (EXECUTED_PLANS == NULL)
			tryToFetchNextPlans();

		if (EXECUTED_PLANS != NULL) {
			if (!Steppers::fillSchedule(group1, EXECUTED_PLANS)) {
				// executed plans were finished
				Serial.print('F');
				freeFinishedPlans();
				continue;
			}
		}

		//serial communication handling
		if (ARRIVAL_BUFFER_INDEX == INSTRUCTION_SIZE) {
			//we received an instrution - do processing
			processControllerInstruction();
		}
		else if (Serial.available()) {
			//new data byte arrived
			ARRIVAL_BUFFER[ARRIVAL_BUFFER_INDEX++] = (byte)Serial.read();
			LAST_BYTE_ARRIVAL_TIME = millis();
		}
		else if (ARRIVAL_BUFFER_INDEX > 0 && (millis() - LAST_BYTE_ARRIVAL_TIME) > 2) {
			//we are interested in consequent messages only
			ARRIVAL_BUFFER_INDEX = 0;
			Serial.print('E'); //incomplete message erased
		}
	}
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
		digitalWrite(STEP_DIR_PIN, LOW);
		digitalWrite(STEP_CLK_PIN, HIGH);
		delayMicroseconds(tone);
		digitalWrite(STEP_CLK_PIN, LOW);

		digitalWrite(STEP_DIR_PIN, HIGH);
		digitalWrite(STEP_CLK_PIN, HIGH);
		delayMicroseconds(tone);
		digitalWrite(STEP_CLK_PIN, LOW);
	}
}

void demo() {

	Plan** plans;
	uint32_t totalSteps = 400 * 10L;



	byte activations[8] = { 2,0,2,2,0,0,0,0 };
	//int16_t timing[8] = { 7501,5626,4762,5952,7501,5626,4688,3536 };
	int16_t interval = 65000;
	int16_t timing[8] = { interval,interval,interval,interval,interval,interval,interval,interval };
	int stepCount = 2;
	byte dirMask = 2;

	Steppers::directScheduleFill(activations, timing, stepCount);
	Steppers::startScheduler();

	/*plans = new Plan*[1]{
		new ConstantPlan(400, 2 * 350, 0, 0)
	};
	Steppers::runPlanning(group1, plans);*/

	/*
	plans = new Plan*[1]{
			new AccelerationPlan(580, START_DELTA_T * 2,52)
	};
	Steppers::runPlanning(group1, plans);

	totalSteps -= 2 * 580;
	while (totalSteps > 0) {
		int16_t nextSteps = min(30000, totalSteps);

		plans = new Plan*[1]{
			new ConstantPlan(nextSteps, 2 * 100, 0, 0)
		};
		Steppers::runPlanning(group1, plans);

		totalSteps -= nextSteps;
	}

	plans = new Plan*[1]{
		new AccelerationPlan(580, 2 * 100,-580 - 52)
	};
	Steppers::runPlanning(group1, plans);*/
	delay(1000);
}

// Processes buffer with instruction from controller - return false on invalid instruction
inline bool processControllerInstruction() {
	const byte* buffer = ARRIVAL_BUFFER;
	ARRIVAL_BUFFER_INDEX = 0;


	uint16_t checksum = 0;
	for (int i = 0; i < INSTRUCTION_SIZE - 2; ++i) {
		checksum += ARRIVAL_BUFFER[i];
	}

	uint16_t receivedChecksum = READ_UINT16(ARRIVAL_BUFFER, INSTRUCTION_SIZE - 2);
	if (receivedChecksum != checksum) {
		//we received invalid instruction
		Serial.print('C'); //invalid checksum
		return false;
	}

	//parse the instruction
	byte command = buffer[0];
	switch (command)
	{
	case 'A':
		//acceleration plan arrived
		return tryAcceptAcceleration(buffer + 1);
	case 'C':
		//constant plan arrived
		return tryAcceptConstantPlan(buffer + 1);
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
	return PLANS_BUFFER[ARRIVAL_PLAN_INDEX] == NULL;
}

bool tryAcceptAcceleration(const byte* buffer) {
	if (!canAddPlan())
	{
		sendPlanOverflow();
		return false;
	}
	else {
		sendPlanAccepted();
	}
	int16_t stepCount = READ_INT16(buffer, 0);
	uint16_t initialDeltaT = READ_UINT16(buffer, 2);
	int16_t n = READ_INT16(buffer, 2 + 2);

	AccelerationPlan* plan = new AccelerationPlan(stepCount, initialDeltaT, n);
	PLANS_BUFFER[ARRIVAL_PLAN_INDEX++] = new Plan*[1]{ plan };
	ARRIVAL_PLAN_INDEX = ARRIVAL_PLAN_INDEX % PLANS_BUFFER_SIZE;

	return true;
}

bool tryAcceptConstantPlan(const byte* buffer) {
	if (!canAddPlan())
	{
		sendPlanOverflow();
		return false;
	}
	else {
		sendPlanAccepted();
	}

	int16_t stepCount = READ_INT16(buffer, 0);
	uint16_t baseDeltaT = READ_UINT16(buffer, 2);
	uint16_t periodNumerator = READ_UINT16(buffer, 2 + 2);
	uint16_t periodDenominator = READ_UINT16(buffer, 2 + 2 + 2);

	ConstantPlan* plan = new ConstantPlan(stepCount, baseDeltaT, periodNumerator, periodDenominator);
	PLANS_BUFFER[ARRIVAL_PLAN_INDEX++] = new Plan*[1]{ plan };
	ARRIVAL_PLAN_INDEX = ARRIVAL_PLAN_INDEX % PLANS_BUFFER_SIZE;
	return true;
}

void sendPlanOverflow() {
	Serial.print('O');
}

void sendPlanAccepted() {
	Serial.print('Y');
}

inline void freeFinishedPlans() {
	//free memory
	for (int i = 0; i < group1.StepperCount; ++i) {
		delete EXECUTED_PLANS[i];
	}

	delete EXECUTED_PLANS;
	EXECUTED_PLANS = NULL;
}

void tryToFetchNextPlans() {
	if (PLANS_BUFFER[NEXT_PLAN_INDEX] == NULL)
		//no more plans available
		return;

	EXECUTED_PLANS = PLANS_BUFFER[NEXT_PLAN_INDEX];
	PLANS_BUFFER[NEXT_PLAN_INDEX] = NULL;
	NEXT_PLAN_INDEX = (NEXT_PLAN_INDEX + 1) % PLANS_BUFFER_SIZE;
	Steppers::initPlanning(group1, EXECUTED_PLANS);
}

