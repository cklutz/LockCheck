#!/bin/bash

# Variables
GIST_ID=$1
FILE=$2
MAX_RETRIES=10          # Maximum number of retries
RETRY_DELAY=1           # Delay between retries in seconds
SCRIPT_NAME=$(basename $0)

for ((i=1; i<=MAX_RETRIES; i++)); do
    echo "$SCRIPT_NAME: Attempt $i of $MAX_RETRIES to update gist..."

    # Try to update the gist
    OUTPUT=$(gh gist edit "$GIST_ID" -f "$FILE" 2>&1)
    EXIT_CODE=$?

    # Check if the update succeeded
    if [[ $EXIT_CODE -eq 0 ]]; then
        echo $OUTPUT
        exit 0
    fi

    # Check if it's a 409 Conflict
    if echo "$OUTPUT" | grep -q "HTTP 409"; then
        # Increase delay if tried to often already
        if [[ $i -ge 7 ]]; then
            RETRY_DELAY=$((RETRY_DELAY * 5))
        elif [[ $i -ge 5 ]]; then
            RETRY_DELAY=$((RETRY_DELAY * 2))
        fi
        echo "$SCRIPT_NAME: Received HTTP 409 conflict. Retrying in $RETRY_DELAY seconds..."
        sleep $RETRY_DELAY
    else
        # Exit if it's any other error
        echo $OUTPUT
        exit $EXIT_CODE
    fi
done

echo "$SCRIPT_NAME: Failed to update gist after $MAX_RETRIES attempts."
echo $OUTPUT
exit 1
