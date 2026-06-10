#!/bin/bash

echo "Current Directory $PWD" 

# Configuration
CONFIG_FILE="$PWD/build-csharp.yaml"
INPUT_FILE="$PWD/openapi.yml"
OUTPUT_DIR="$PWD/Client"
NAMESPACE="DocuSeal.Api"
CLIENT_NAME="DocuSeal"
GENERATOR_PROJECT="src/OpenApiClientGenerator.Core"

cd ~/DEVELOPMENT/github/custom-openapi-client-generator

# Test script for C# client generation
# Generates a C# client from OpenAPI spec, builds it, and verifies expectations

# Don't use set -e since we want to track failures but continue testing

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Counters
TESTS_PASSED=0
TESTS_FAILED=0

# Helper functions
print_header() {
    echo ""
    echo "=========================================="
    echo "$1"
    echo "=========================================="
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
    ((TESTS_PASSED++))
}

print_failure() {
    echo -e "${RED}✗ $1${NC}"
    ((TESTS_FAILED++))
}

print_info() {
    echo -e "${YELLOW}→ $1${NC}"
}

check_file_exists() {
    local file="$1"
    local description="$2"
    if [ -f "$file" ]; then
        print_success "$description exists: $file"
        return 0
    else
        print_failure "$description missing: $file"
        return 1
    fi
}

check_dir_exists() {
    local dir="$1"
    local description="$2"
    if [ -d "$dir" ]; then
        print_success "$description exists: $dir"
        return 0
    else
        print_failure "$description missing: $dir"
        return 1
    fi
}

check_file_contains() {
    local file="$1"
    local pattern="$2"
    local description="$3"
    if grep -q "$pattern" "$file" 2>/dev/null; then
        print_success "$description"
        return 0
    else
        print_failure "$description (pattern not found: $pattern)"
        return 1
    fi
}

print_header "C# Client Generation Test"

# Step 1: Clean previous output
print_header "Step 1: Cleaning previous output"
if [ -d "$OUTPUT_DIR" ]; then
    print_info "Removing existing output directory..."
    rm -rf "$OUTPUT_DIR"
    print_success "Previous output cleaned"
else
    print_info "No previous output to clean"
fi

# Step 2: Run the generator
print_header "Step 2: Running the generator"
print_info "Using configuration: $CONFIG_FILE"
print_info "Input: $INPUT_FILE"
print_info "Output: $OUTPUT_DIR"

GENERATOR_OUTPUT=$(dotnet run --project "$GENERATOR_PROJECT" -- "$CONFIG_FILE" --input "$INPUT_FILE" --output "$OUTPUT_DIR" --namespace "$NAMESPACE" --client-name "$CLIENT_NAME" 2>&1)
GENERATOR_EXIT_CODE=$?

if [ "$GENERATOR_EXIT_CODE" -eq 0 ]; then
    print_success "Generator completed successfully"
    # Show summary line
    echo "$GENERATOR_OUTPUT" | grep -E "Found [0-9]+ models and [0-9]+ operations" || true
else
    print_failure "Generator failed (exit code: $GENERATOR_EXIT_CODE)"
    echo "$GENERATOR_OUTPUT" | tail -20
    exit 1
fi

# Step 3: Verify output structure
print_header "Step 3: Verifying output structure"

check_dir_exists "$OUTPUT_DIR" "Output directory"
check_dir_exists "$OUTPUT_DIR/Models" "Models directory"
check_dir_exists "$OUTPUT_DIR/Serializers" "Serializers directory"
check_dir_exists "$OUTPUT_DIR/Converters" "Converters directory"
check_dir_exists "$OUTPUT_DIR/Auth" "Auth directory"
check_dir_exists "$OUTPUT_DIR/Clients" "Clients directory"

# Step 4: Verify key files exist
print_header "Step 4: Verifying key files"

check_file_exists "$OUTPUT_DIR/PetStoreClient.csproj" "Project file"
check_file_exists "$OUTPUT_DIR/Serializers/BaseSerializer.cs" "Base serializer"
check_file_exists "$OUTPUT_DIR/Auth/BearerAuthHandler.cs" "Bearer auth handler"
check_file_exists "$OUTPUT_DIR/Auth/BasicAuthHandler.cs" "Basic auth handler"
check_file_exists "$OUTPUT_DIR/Auth/ApiKeyAuthHandler.cs" "API key auth handler"
check_file_exists "$OUTPUT_DIR/Auth/OAuthHandler.cs" "OAuth handler"

# Step 5: Verify some generated models exist
print_header "Step 5: Verifying generated models"

MODEL_COUNT=$(find "$OUTPUT_DIR/Models" -name "*.cs" 2>/dev/null | wc -l)
if [ "$MODEL_COUNT" -gt 0 ]; then
    print_success "Found $MODEL_COUNT model files"
else
    print_failure "No model files generated"
fi

SERIALIZER_COUNT=$(find "$OUTPUT_DIR/Serializers" -name "*.cs" 2>/dev/null | wc -l)
if [ "$SERIALIZER_COUNT" -gt 0 ]; then
    print_success "Found $SERIALIZER_COUNT serializer files"
else
    print_failure "No serializer files generated"
fi

CLIENT_COUNT=$(find "$OUTPUT_DIR/Clients" -name "*.cs" 2>/dev/null | wc -l)
if [ "$CLIENT_COUNT" -gt 0 ]; then
    print_success "Found $CLIENT_COUNT client files"
else
    print_failure "No client files generated"
fi

# Step 6: Verify project file content
print_header "Step 6: Verifying project file content"

check_file_contains "$OUTPUT_DIR/PetStoreClient.csproj" "net8.0" "Project targets .NET 8.0"
check_file_contains "$OUTPUT_DIR/PetStoreClient.csproj" "System.Text.Json" "Project references System.Text.Json"
check_file_contains "$OUTPUT_DIR/PetStoreClient.csproj" "PetStoreApi" "Project has correct namespace"

# Step 7: Verify namespace in generated code
print_header "Step 7: Verifying namespace in generated code"

# Check a model file
SAMPLE_MODEL=$(find "$OUTPUT_DIR/Models" -name "*.cs" | head -1)
if [ -n "$SAMPLE_MODEL" ]; then
    check_file_contains "$SAMPLE_MODEL" "namespace PetStoreApi" "Model has correct namespace"
fi

# Check base serializer
check_file_contains "$OUTPUT_DIR/Serializers/BaseSerializer.cs" "namespace PetStoreApi" "Serializer has correct namespace"

# Step 8: Build the generated client
print_header "Step 8: Building the generated client"

print_info "Running dotnet build..."
BUILD_OUTPUT=$(dotnet build "$OUTPUT_DIR/PetStoreClient.csproj" --nologo 2>&1)
BUILD_EXIT_CODE=$?

# Count errors and warnings
ERROR_COUNT=$(echo "$BUILD_OUTPUT" | grep -E "^\s*[0-9]+ Error\(s\)" | grep -oE "[0-9]+" | head -1 || echo "0")
WARNING_COUNT=$(echo "$BUILD_OUTPUT" | grep -E "^\s*[0-9]+ Warning\(s\)" | grep -oE "[0-9]+" | head -1 || echo "0")

if [ "$BUILD_EXIT_CODE" -eq 0 ]; then
    print_success "Generated client builds successfully"
else
    print_failure "Generated client failed to build (exit code: $BUILD_EXIT_CODE)"
    echo ""
    echo "Build errors:"
    echo "$BUILD_OUTPUT" | grep -E ": error " | head -10
fi

# Step 9: Report build statistics
print_header "Step 9: Build statistics"

if [ "$ERROR_COUNT" = "0" ] || [ -z "$ERROR_COUNT" ]; then
    print_success "No build errors"
else
    print_failure "Found $ERROR_COUNT build error(s)"
    echo ""
    echo "First few errors:"
    echo "$BUILD_OUTPUT" | grep -E ": error " | head -5
fi

if [ "$WARNING_COUNT" = "0" ] || [ -z "$WARNING_COUNT" ]; then
    print_success "No build warnings"
else
    print_info "Found $WARNING_COUNT build warning(s) - review if needed"
    echo ""
    echo "Warning categories:"
    echo "$BUILD_OUTPUT" | grep -E ": warning " | sed 's/.*warning \([A-Z0-9]*\):.*/\1/' | sort | uniq -c | sort -rn | head -5
fi

# Step 10: Verify generated code patterns
print_header "Step 10: Verifying code patterns"

# Check that models have proper JSON serialization attributes
SAMPLE_MODEL=$(find "$OUTPUT_DIR/Models" -name "*.cs" | head -1)
if [ -n "$SAMPLE_MODEL" ]; then
    if grep -q "JsonPropertyName" "$SAMPLE_MODEL" 2>/dev/null; then
        print_success "Models use JsonPropertyName attributes"
    else
        print_info "Models may not require JsonPropertyName attributes"
    fi
fi

# Check that clients use HttpClient
SAMPLE_CLIENT=$(find "$OUTPUT_DIR/Clients" -name "*Client.cs" ! -name "I*" | head -1)
if [ -n "$SAMPLE_CLIENT" ]; then
    check_file_contains "$SAMPLE_CLIENT" "HttpClient" "Client uses HttpClient"
    check_file_contains "$SAMPLE_CLIENT" "async Task" "Client uses async/await pattern"
fi

# Check that auth handlers extend DelegatingHandler
check_file_contains "$OUTPUT_DIR/Auth/BearerAuthHandler.cs" "DelegatingHandler" "Auth handlers use DelegatingHandler"

# Summary
print_header "Test Summary"
echo ""
echo -e "Tests Passed: ${GREEN}$TESTS_PASSED${NC}"
echo -e "Tests Failed: ${RED}$TESTS_FAILED${NC}"
echo ""

if [ "$TESTS_FAILED" -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}Some tests failed. Please review the output above.${NC}"
    exit 1
fi
