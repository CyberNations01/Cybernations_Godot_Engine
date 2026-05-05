CXX      = g++
CXXFLAGS = -std=c++17 -Wall -Wextra -I./includes
SRCDIR   = src
OBJDIR   = obj
LOAD_TARGET      = out/testLoadJson
ADAPT_TARGET     = out/testAdaptSimulation
CATEGORY_TARGET  = out/testDisruptionCategories
TRAVERSE_TARGET  = out/testTraverseDisruption


SOURCES = $(SRCDIR)/core/Params.cpp \
          $(SRCDIR)/core/FeedbackPool.cpp \
          $(SRCDIR)/core/FeedbackTokenManager.cpp \
          $(SRCDIR)/core/Stack.cpp \
          $(SRCDIR)/core/Player.cpp \
          $(SRCDIR)/core/DisruptionCard.cpp \
          $(SRCDIR)/core/DataLoader.cpp \
          $(SRCDIR)/core/Goal.cpp \
          $(SRCDIR)/core/Tile.cpp \
          $(SRCDIR)/game/GameState.cpp \
          $(SRCDIR)/game/GameRoom.cpp \
          $(SRCDIR)/game/RoundController.cpp \
          $(SRCDIR)/phase/EnvisionPhaseHandler.cpp \
          $(SRCDIR)/phase/TraversePhaseHandler.cpp \
          $(SRCDIR)/phase/AdoptPhaseHandler.cpp \
          $(SRCDIR)/game/GameUtility.cpp \
		  $(SRCDIR)/net/Room.cpp \

#           $(SRCDIR)/test/EnvisionPhaseTest.cpp


COMMON_OBJECTS = $(patsubst $(SRCDIR)/%.cpp, $(OBJDIR)/%.o, $(SOURCES))
LOAD_OBJECTS = $(COMMON_OBJECTS) $(OBJDIR)/test/testLoadJson.o
ADAPT_OBJECTS = $(COMMON_OBJECTS) $(OBJDIR)/test/testAdaptSimulation.o
CATEGORY_OBJECTS = $(COMMON_OBJECTS) $(OBJDIR)/test/testDisruptionCategories.o
TRAVERSE_OBJECTS = $(COMMON_OBJECTS) $(OBJDIR)/test/testTraverseDisruption.o
 
all: $(LOAD_TARGET)

$(LOAD_TARGET): $(LOAD_OBJECTS)
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -o $@ $^

$(ADAPT_TARGET): $(ADAPT_OBJECTS)
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -o $@ $^

$(CATEGORY_TARGET): $(CATEGORY_OBJECTS)
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -o $@ $^

$(TRAVERSE_TARGET): $(TRAVERSE_OBJECTS)
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -o $@ $^
 
$(OBJDIR)/%.o: $(SRCDIR)/%.cpp
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -c $< -o $@
 

SERVER_TARGET = out/server
SERVER_OBJECTS = $(COMMON_OBJECTS) $(OBJDIR)/net/http-server.o
ROOM_SERVER_TARGET = out/room-server
ROOM_SERVER_OBJECTS = $(COMMON_OBJECTS) $(OBJDIR)/net/room-http-server.o

server: $(SERVER_TARGET)
room-server: $(ROOM_SERVER_TARGET)

$(SERVER_TARGET): $(SERVER_OBJECTS)
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -o $@ $^ -lpthread

$(ROOM_SERVER_TARGET): $(ROOM_SERVER_OBJECTS)
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -o $@ $^ -lpthread

clean:
	rm -rf $(OBJDIR) $(LOAD_TARGET) $(ADAPT_TARGET) $(CATEGORY_TARGET) $(TRAVERSE_TARGET) $(SERVER_TARGET) $(ROOM_SERVER_TARGET)

adapt-sim: $(ADAPT_TARGET)
disruption-categories: $(CATEGORY_TARGET)
traverse-test: $(TRAVERSE_TARGET)

test-all: $(ADAPT_TARGET) $(CATEGORY_TARGET) $(TRAVERSE_TARGET)
	./$(CATEGORY_TARGET)
	./$(TRAVERSE_TARGET)
	./$(ADAPT_TARGET)

.PHONY: all clean adapt-sim disruption-categories traverse-test test-all server room-server