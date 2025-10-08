using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynCustomAnalyzer.SourceGenerator
{
    public partial class VfxSystemGenerator
    {
        private void GenerateSystem(GeneratorExecutionContext context, StructDeclarationSyntax structDeclaration, string menuName, string systemType, int capacity)
        {
            var structName = structDeclaration.Identifier.Text;
            var singletonName = $"VFX{menuName}Singleton";
            var systemName = $"VFX{menuName}System";
            var managerFieldName = $"_{menuName.ToLower()}Manager";

            string source;

            if (systemType == "Parentless")
            {
                source = GetParentlessSystemSource(structName, singletonName, systemName, managerFieldName, capacity);
            }
            else
            {
                source = GetParentedSystemSource(structName, singletonName, systemName, managerFieldName, capacity);
            }

            context.AddSource($"{systemName}.g.cs", source);
        }

        private string GetParentlessSystemSource(string structName, string singletonName, string systemName, string managerFieldName, int capacity)
        {
            return $@"using Core.Scripts.Systems.External.Vfx.Base;
using Core.Scripts.Systems.External.Vfx.Base.Utils;
using Core.Scripts.Systems.External.Vfx.Types;
using Unity.Entities;
using UnityEngine;

namespace Core.Scripts.Systems.External.Vfx
{{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct {systemName} : ISystem
    {{
        private int _spawnBatchId;
        private int _requestsCountId;
        private int _requestsBufferId;

        private VFXManager<{structName}> {managerFieldName};
        private EntityQuery _singletonQuery;

        private const int Capacity = {capacity};

        public void OnCreate(ref SystemState state)
        {{
            _spawnBatchId = Shader.PropertyToID(""SpawnBatch"");
            _requestsCountId = Shader.PropertyToID(""SpawnRequestsCount"");
            _requestsBufferId = Shader.PropertyToID(""SpawnRequestsBuffer"");

            {managerFieldName} = new VFXManager<{structName}>(Capacity, ref VfxParentlessReferences<{singletonName}>.RequestsBuffer);

            _singletonQuery = state.GetEntityQuery(ComponentType.ReadOnly<{singletonName}>());

            state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), new {singletonName}
            {{
                Manager = {managerFieldName},
            }});
        }}

        public void OnDestroy(ref SystemState state)
        {{
            {managerFieldName}.Dispose(ref VfxParentlessReferences<{singletonName}>.RequestsBuffer);
        }}

        public void OnUpdate(ref SystemState state)
        {{
            _singletonQuery.CompleteDependency();

            var rateRatio = state.WorldUnmanaged.Time.DeltaTime / Time.deltaTime;

            {managerFieldName}.Update(
                VfxParentlessReferences<{singletonName}>.VfxGraph,
                ref VfxParentlessReferences<{singletonName}>.RequestsBuffer,
                rateRatio,
                _spawnBatchId,
                _requestsCountId,
                _requestsBufferId);
        }}
    }}
}}";
        }

        private string GetParentedSystemSource(string structName, string singletonName, string systemName, string managerFieldName, int capacity)
        {
            return $@"using Core.Scripts.Systems.External.Vfx.Base;
using Core.Scripts.Systems.External.Vfx.Base.Utils;
using Core.Scripts.Systems.External.Vfx.Types;
using Unity.Entities;
using UnityEngine;

namespace Core.Scripts.Systems.External.Vfx
{{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct {systemName} : ISystem
    {{
        private int _spawnBatchId;
        private int _requestsCountId;
        private int _requestsBufferId;
        private int _dataBufferId;

        private VFXManagerParented<{structName}> {managerFieldName};
        private EntityQuery _singletonQuery;

        private const int Capacity = {capacity};

        public void OnCreate(ref SystemState state)
        {{
            _spawnBatchId = Shader.PropertyToID(""SpawnBatch"");
            _requestsCountId = Shader.PropertyToID(""SpawnRequestsCount"");
            _requestsBufferId = Shader.PropertyToID(""SpawnRequestsBuffer"");
            _dataBufferId = Shader.PropertyToID(""DataBuffer"");

            {managerFieldName} = new VFXManagerParented<{structName}>(Capacity,
                ref VfxParentedReferences<{singletonName}>.RequestsBuffer,
                ref VfxParentedReferences<{singletonName}>.DataBuffer);

            _singletonQuery = state.GetEntityQuery(ComponentType.ReadOnly<{singletonName}>());

            state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), new {singletonName}
            {{
                Manager = {managerFieldName},
            }});
        }}

        public void OnDestroy(ref SystemState state)
        {{
            {managerFieldName}.Dispose(ref VfxParentedReferences<{singletonName}>.RequestsBuffer,
                ref VfxParentedReferences<{singletonName}>.DataBuffer);
        }}

        public void OnUpdate(ref SystemState state)
        {{
            _singletonQuery.CompleteDependency();

            var rateRatio = state.WorldUnmanaged.Time.DeltaTime / Time.deltaTime;

            {managerFieldName}.Update(
                VfxParentedReferences<{singletonName}>.VfxGraph,
                ref VfxParentedReferences<{singletonName}>.RequestsBuffer,
                ref VfxParentedReferences<{singletonName}>.DataBuffer,
                rateRatio,
                _spawnBatchId,
                _requestsCountId,
                _requestsBufferId,
                _dataBufferId);
        }}
    }}
}}";
        }
    }
}