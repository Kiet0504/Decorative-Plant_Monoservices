#!/bin/sh
# Build solution - ignore file arguments passed by lint-staged

SOLUTION_FILE="DecorativePlant-BE.sln"

# Restore packages first if needed, then build
dotnet restore "$SOLUTION_FILE" --verbosity:quiet || true
dotnet build "$SOLUTION_FILE" --verbosity:quiet
