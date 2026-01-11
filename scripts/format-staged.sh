#!/bin/sh
# Format staged .NET files using dotnet format

SOLUTION_FILE="DecorativePlant-BE.sln"

if [ -z "$1" ]; then
  exit 0
fi

# Build the --include argument with all file paths (handle Windows paths)
INCLUDE_ARGS=""
for file in "$@"; do
  # Convert Windows path separators if needed and normalize path
  normalized_file=$(echo "$file" | sed 's|\\|/|g')
  if [ -z "$INCLUDE_ARGS" ]; then
    INCLUDE_ARGS="$normalized_file"
  else
    INCLUDE_ARGS="$INCLUDE_ARGS $normalized_file"
  fi
done

# Format whitespace, style, and analyzers for the staged files (suppress all output for speed)
dotnet format "$SOLUTION_FILE" whitespace --include $INCLUDE_ARGS --verbosity quiet > /dev/null 2>&1
dotnet format "$SOLUTION_FILE" style --include $INCLUDE_ARGS --verbosity quiet > /dev/null 2>&1
dotnet format "$SOLUTION_FILE" analyzers --include $INCLUDE_ARGS --verbosity quiet > /dev/null 2>&1
