# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

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

# Configuration
CONFIG_FILE="build.yaml"
INPUT_FILE="openapi.json"
OUTPUT_DIR="output"
NAMESPACE="ZipTax"
CLIENT_NAME="ZipTax"
GENERATOR_PROJECT="/Users/felipe/DEVELOPMENT/github/custom-openapi-client-generator/src/OpenApiClientGenerator.Core"

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

cd output
dotnet restore
cd ..