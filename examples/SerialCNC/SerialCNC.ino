#include "StepperControl.h"

// how many bytes contains instruction from controller
#define INSTRUCTION_SIZE 12 
#define PLANS_BUFFER_SIZE 8 

#define READ_INT16(array, position) (int16_t)(array[position] << 8 + array[position + 1])
#define READ_UINT16(array, position) (uint16_t)(array[position] << 8 + array[position + 1])

int STEP_CLK_PIN = 8;
int STEP_DIR_PIN = 9;


Plan** PLANS_BUFFER[PLANS_BUFFER_SIZE]; //here buffered plans will be stored
byte NEXT_PLAN_INDEX = 0; //index to next plan that will be scheduled
byte ARRIVAL_PLAN_INDEX = 0; //index where new plan can arrive
Plan** EXECUTED_PLANS = NULL;

StepperGroup group1 = StepperGroup(1, new byte[1]{ STEP_CLK_PIN }, new byte[1]{ STEP_DIR_PIN });

void setup() {
	Serial.begin(128000);

	pinMode(13, OUTPUT);
	pinMode(STEP_CLK_PIN, OUTPUT);
	pinMode(STEP_DIR_PIN, OUTPUT);

	Steppers::initialize();
}

void demo() {
	Plan** plans;
	uint32_t totalSteps = 400 * 1000L;

	plans = new Plan*[1]{
		new AccelerationPlan(625, 1, 1, START_DELTA_T)
	};
	Steppers::runPlanning(group1, plans);

	while (totalSteps > 0) {
		int16_t nextSteps = min(30000, totalSteps);

		plans = new Plan*[1]{
			new ConstantPlan(nextSteps, 100, 0)
		};
		Steppers::runPlanning(group1, plans);

		totalSteps -= nextSteps;
	}

	plans = new Plan*[1]{
		new AccelerationPlan(625, -1, 1, 100)
	};
	Steppers::runPlanning(group1, plans);
	delay(1000);
}

// Processes buffer with instruction from controller - return false on invalid instruction
inline bool processControllerInstruction(byte* buffer) {
	uint16_t checksum = 0;
	for (int i = 0; i < INSTRUCTION_SIZE; ++i) {
		checksum += buffer[i];
	}

	uint16_t receivedChecksum = READ_UINT16(buffer, INSTRUCTION_SIZE - 2);
	if (receivedChecksum != checksum)
		//we received invalid instruction
		return false;

	bool canAddPlan = PLANS_BUFFER[ARRIVAL_PLAN_INDEX] == NULL;

	//parse the instruction
	byte command = buffer[0];
	switch (command)
	{
	case 'A': {
		//acceleration plan arrived
		if (!canAddPlan)return true;

		int16_t stepCount = READ_INT16(buffer, 1);
		int16_t accelerationNumerator = READ_INT16(buffer, 1 + 2);
		int16_t accelerationDenominator = READ_INT16(buffer, 1 + 2 + 2);
		uint16_t initialDeltaT = READ_UINT16(buffer, 1 + 2 + 2 + 2);

		AccelerationPlan* plan = new AccelerationPlan(stepCount, accelerationNumerator, accelerationDenominator, initialDeltaT);
		PLANS_BUFFER[ARRIVAL_PLAN_INDEX++] = new Plan*[1]{ plan };
		ARRIVAL_PLAN_INDEX = ARRIVAL_PLAN_INDEX % PLANS_BUFFER_SIZE;
		break;
	}
	case 'C': {
		//constant plan arrived
		if (!canAddPlan)return true;

		int16_t stepCount = READ_INT16(buffer, 1);
		uint16_t baseDeltaT = READ_UINT16(buffer, 1 + 2);
		uint16_t period = READ_UINT16(buffer, 1 + 2 + 2);

		ConstantPlan* plan = new ConstantPlan(stepCount, baseDeltaT, period);
		PLANS_BUFFER[ARRIVAL_PLAN_INDEX++] = new Plan*[1]{ plan };
		ARRIVAL_PLAN_INDEX = ARRIVAL_PLAN_INDEX % PLANS_BUFFER_SIZE;
		break;
	}
	default:
		//unknown command
		return false;
	}

	return true; //instruction was processed (but still can be kept within the buffer for repetitive processing)
}

inline void freeFinishedPlans() {
	//free memory
	for (int i = 0; i < group1.StepperCount; ++i) {
		delete EXECUTED_PLANS[i];
	}

	delete EXECUTED_PLANS;
}

inline void tryToFetchNextPlans() {
	if (PLANS_BUFFER[NEXT_PLAN_INDEX] == NULL)
		//no more plans available
		return;

	//fetch new plans
	EXECUTED_PLANS = PLANS_BUFFER[NEXT_PLAN_INDEX];
	PLANS_BUFFER[NEXT_PLAN_INDEX] = NULL;
	NEXT_PLAN_INDEX = (NEXT_PLAN_INDEX + 1) & 8;
}

void loop() {
	digitalWrite(13, HIGH);

	demo();
	return;

	byte arrivalBuffer[INSTRUCTION_SIZE];
	byte arrivalBufferIndex = 0;

	for (;;) {
		if (EXECUTED_PLANS == NULL)
			tryToFetchNextPlans();

		if (EXECUTED_PLANS != NULL) {
			if (!Steppers::fillSchedule(group1, EXECUTED_PLANS)) {
				// executed plans were finished
				freeFinishedPlans();
				continue;
			}
		}

		//serial communication handling
		if (arrivalBufferIndex == INSTRUCTION_SIZE) {
			//we received an instrution - do processing
			processControllerInstruction(arrivalBuffer);
			//TODO request repetition when data corrupted
		}
		else if (Serial.available()) {
			//new data byte arrived
			arrivalBuffer[arrivalBufferIndex++] = (byte)Serial.read();
		}

	}
}
