# Microsoft.OpenApi v3.0 Upgrade Plan

## Current State

**Current Version**: Microsoft.OpenApi 1.6.14 (via git submodule)
- Location: `/workspace/OpenAPI.NET` (git submodule)
- Remote: `git@github.com:crochik/OpenAPI.NET.git`
- Current branch: `master` (tracking `vnext`)
- Package references: Commented out in PI.OpenAPI.csproj, active in PI.Shared.Services.OpenApiGenerator.csproj

**Target Version**: Microsoft.OpenApi 3.0.0 (NuGet package)
- Use official NuGet packages instead of git submodule
- Adds support for OpenAPI Specification v3.2
- Includes breaking changes to media types and XML representation

## Impact Assessment

### ✅ Low Risk Areas (No changes required)

1. **No OpenApiMediaType direct usage**: Code only accesses `.Content` dictionaries via iteration
2. **No OpenApiXml usage**: No XML representation code found
3. **No custom visitor implementations**: No IOpenApiVisitor implementations to update
4. **Serialization methods**: `SerializeAsV3()` and `SerializeAsV3WithoutReference()` remain compatible

### ⚠️ Medium Risk Areas (Potential issues)

1. **Content property access** (`Parser/OpenApiParser.cs`):
   - Lines: 649, 792, 795, 863
   - Accesses `.Content` on responses and request bodies
   - Impact: Now returns `IDictionary<string, IOpenApiMediaType>` instead of concrete type
   - Risk: LOW - Code only iterates and accesses `.Schema` property (interface compatible)

2. **Response payloads** (`Services/ActionService.cs:318-322`):
   - Accesses response.Payloads which internally uses Content
   - Risk: LOW - Uses internal models, not OpenAPI types directly

3. **OpenAPI object instantiation** (`PI.Shared.Services.OpenApiGenerator/OpenApiSpecGenerator.cs`):
   - Creates many OpenAPI objects: OpenApiSchema, OpenApiReference, OpenApiSecurityScheme, etc.
   - Risk: LOW - These constructors remain compatible

### 🔍 Files Using Microsoft.OpenApi

**PI.OpenAPI project:**
- `Parser/OpenApiParser.cs` - Main parser, uses OpenApiDocument, OpenApiSchema, OpenApiDiagnostic
- `Writer/ObjectWriter.cs` - Custom IOpenApiWriter implementation
- `Controllers/GenerateController.cs` - Uses Document.Serialize()

**PI.Shared.Services.OpenApiGenerator:**
- `OpenApiSpecGenerator.cs` - Generates OpenAPI documents, heavy usage
- Extension files: OpenApiDocumentExtensions, OpenApiOperationExtensions, etc.

## Breaking Changes from Upgrade Guide

### 1. Media Type Interface Change
```csharp
// Before (v2.x)
public IDictionary<string, OpenApiMediaType>? Content { get; set; }

// After (v3.0)
public IDictionary<string, IOpenApiMediaType>? Content { get; set; }
```
**Impact**: None - Code doesn't declare Content property types

### 2. XML Object Refactoring
```csharp
// Before
var xml = new OpenApiXml { Attribute = true, Wrapped = false };

// After
var xml = new OpenApiXml { NodeType = OpenApiXmlNodeType.Attribute };
```
**Impact**: None - No OpenApiXml usage found

### 3. Visitor Pattern Changes
```csharp
// Before
public override void Visit(OpenApiMediaType mediaType) { }

// After
public override void Visit(IOpenApiMediaType mediaType) { }
```
**Impact**: None - No custom visitor implementations

## Upgrade Steps

### Phase 1: Update Package References (15-30 minutes)

1. **Create feature branch**
   ```bash
   cd /workspace
   git checkout -b upgrade/openapi-v3
   ```

2. **Update PI.Shared.Services.OpenApiGenerator.csproj**

   Remove ProjectReferences (lines 4-5):
   ```xml
   <!-- REMOVE THESE LINES -->
   <ProjectReference Include="../OpenAPI.NET/src/Microsoft.OpenApi/Microsoft.OpenApi.csproj"/>
   <ProjectReference Include="../OpenAPI.NET/src/Microsoft.OpenApi.Readers/Microsoft.OpenApi.Readers.csproj"/>
   ```

   Update PackageReferences (lines 9-10) from version 2.3.10 to 3.0.0:
   ```xml
   <PackageReference Include="Microsoft.OpenApi" Version="3.0.0" />
   <PackageReference Include="Microsoft.OpenApi.YamlReader" Version="3.0.0" />
   ```

3. **Add Microsoft.OpenApi.Readers package** (if needed)

   The Readers package may need to be explicitly added:
   ```xml
   <PackageReference Include="Microsoft.OpenApi.Readers" Version="3.0.0" />
   ```

4. **Restore NuGet packages**
   ```bash
   cd /workspace
   dotnet restore SchedOnl.sln
   ```

### Phase 2: Build and Test (1-2 hours)

1. **Build the solution**
   ```bash
   cd /workspace
   dotnet build SchedOnl.sln
   ```
   Expected: May have compiler warnings but should build successfully

2. **Run unit tests**
   ```bash
   dotnet test /workspace/UnitTests
   ```
   Focus on OpenAPI-related tests

3. **Test parser functionality**
   - Import a sample OpenAPI spec via DocumentController
   - Verify ObjectTypes are created correctly
   - Check that Operations parse properly
   - Validate reference resolution works

4. **Test generator functionality**
   - Generate OpenAPI spec via GenerateController
   - Verify schemas are created correctly
   - Ensure YAML output is valid OpenAPI v3.0

5. **Test operation execution**
   - Run a FlowRun that triggers OpenApiOperation action
   - Verify ActionService processes correctly
   - Check HTTP callout execution

### Phase 3: Code Review and Adjustments (1-2 hours)

1. **Review Content property usage** - Check `Parser/OpenApiParser.cs:649, 792, 863`
   - Ensure iteration over `.Content` still works
   - Verify `.Schema` property access is compatible

2. **Check serialization output**
   - Compare generated YAML before/after upgrade
   - Ensure no unexpected format changes
   - Validate against OpenAPI validators

3. **Review new capabilities** (Optional enhancements for future)
   - OpenAPI 3.2 support via `SerializeAsV32()`
   - New media type interfaces
   - Enhanced parameter types

### Phase 4: Integration Testing (2-3 hours)

1. **Test with real OpenAPI documents**
   - GitHub integration (RepositoryController)
   - Asana spec (`asana_oas.yaml`)
   - Any integration-specific specs

2. **End-to-end workflow**
   - Upload OpenAPI document
   - Parse into ObjectTypes
   - Create objects from operations
   - Execute operations
   - Validate responses

3. **Performance testing**
   - Parse large OpenAPI documents
   - Generate large schemas
   - Monitor memory usage

### Phase 5: Deployment (Variable)

1. **Update CLAUDE.md** - Add note about OpenAPI.NET v3 usage

2. **Document any workarounds** - If issues found, document solutions

3. **Update deployment artifacts**
   - Rebuild Docker images
   - Update Kubernetes manifests if needed
   - Tag release with version number

## Rollback Plan

If critical issues are found:

1. **Revert package version changes**

   In PI.Shared.Services.OpenApiGenerator.csproj, change back to:
   ```xml
   <PackageReference Include="Microsoft.OpenApi" Version="2.3.10" />
   <PackageReference Include="Microsoft.OpenApi.YamlReader" Version="2.3.10" />
   ```

   Add back the ProjectReferences if they were working:
   ```xml
   <ProjectReference Include="../OpenAPI.NET/src/Microsoft.OpenApi/Microsoft.OpenApi.csproj"/>
   <ProjectReference Include="../OpenAPI.NET/src/Microsoft.OpenApi.Readers/Microsoft.OpenApi.Readers.csproj"/>
   ```

2. **Restore and rebuild**
   ```bash
   dotnet restore SchedOnl.sln
   dotnet build SchedOnl.sln
   # Deploy previous version
   ```

## Risk Mitigation

1. **Backup current state**: Tag current commit before starting
2. **Incremental testing**: Test each component separately
3. **Feature flag**: Consider adding config to switch parser implementations if needed
4. **Staged rollout**: Deploy to test environment first
5. **Monitor logs**: Watch for OpenAPI-related errors post-deployment

## Success Criteria

- ✅ Solution builds without errors
- ✅ All existing unit tests pass
- ✅ Can import OpenAPI v3.0 documents
- ✅ Can generate valid OpenAPI v3.0 specs
- ✅ Operation execution works correctly
- ✅ No regression in existing integrations
- ✅ Performance metrics remain stable

## Estimated Timeline

- **Phase 1 (Update Packages)**: 15-30 minutes
- **Phase 2 (Build/Test)**: 1-2 hours
- **Phase 3 (Review)**: 1-2 hours
- **Phase 4 (Integration)**: 2-3 hours
- **Phase 5 (Deployment)**: Variable

**Total: 4.25-7.5 hours** (< 1 day)

## Notes

- The upgrade is relatively low risk due to minimal use of breaking change areas
- Main risk is in Content property access, but interface compatibility should handle it
- **No code changes expected** - only package reference updates
- Switching from git submodule to NuGet packages simplifies dependency management
- Official Microsoft NuGet packages will be used instead of local submodule
- The OpenAPI.NET git submodule can potentially be removed after successful upgrade

## Future Enhancements (Post-Upgrade)

Once upgraded to v3.0, consider:

1. **OpenAPI 3.2 support**: Implement `SerializeAsV32()` for newer spec version
2. **Enhanced media types**: Leverage new IOpenApiMediaType features
3. **Better error handling**: Use improved diagnostic capabilities
4. **Performance optimizations**: Take advantage of v3 performance improvements
5. **Remove OpenAPI.NET submodule**: If no longer needed, remove the git submodule to simplify repository
   ```bash
   git submodule deinit -f OpenAPI.NET
   git rm -f OpenAPI.NET
   rm -rf .git/modules/OpenAPI.NET
   ```

## References

- Upgrade Guide: https://raw.githubusercontent.com/crochik/OpenAPI.NET/refs/heads/vnext/docs/upgrade-guide-3.md
- OpenAPI.NET GitHub: https://github.com/microsoft/OpenAPI.NET
- OpenAPI Specification: https://github.com/OAI/OpenAPI-Specification
- NuGet Package: https://www.nuget.org/packages/Microsoft.OpenApi/3.0.0
