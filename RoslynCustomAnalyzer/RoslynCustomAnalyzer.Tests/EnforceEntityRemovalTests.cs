using System.Threading.Tasks;
using Xunit;
using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    RoslynCustomAnalyzer.EnforceEntityRemovalAnalyzer>;

namespace RoslynCustomAnalyzer.Tests
{
    public class EnforceEntityRemovalTests
    {
        // Common code stubs for Unity types to make the test code compile.
        private const string Stubs = @"
            using System;
            namespace Unity.Entities
            {
                public interface IComponentData {}
                public interface IEntityMustBeRemoved {}
                public struct Entity {}
                public partial interface IJobEntity {}
                public struct EntityCommandBuffer
                {
                    public struct ParallelWriter
                    {
                        public void DestroyEntity(int index, Entity entity) {}
                        public void AddBuffer<T>(int index, Entity entity) {}
                        public void AppendToBuffer<T>(int index, Entity entity, T value) {}
                    }
                }
            }
            public class BurstCompile : System.Attribute {}
            public class ChunkIndexInQuery : System.Attribute {}
            public class ReadOnly : System.Attribute {}
            public class WithAllAttribute : System.Attribute
            {
                public WithAllAttribute(params System.Type[] types) {}
            }
            public struct HitEventComponent : Unity.Entities.IComponentData, Unity.Entities.IEntityMustBeRemoved { public Unity.Entities.Entity Target { get; } public Unity.Entities.Entity Owner { get; } }
            public struct HealthComponent : Unity.Entities.IComponentData {}
            public struct DamageModule {}
            public struct DynamicBuffer<T> {}
            public struct HealthChangedEvent { public float Value; public Unity.Entities.Entity Invoker; }
            public struct BufferLookup<T> { public bool TryGetBuffer(Unity.Entities.Entity entity, out DynamicBuffer<T> buffer) { buffer = default; return false; } }
            public struct ComponentLookup<T> { public bool TryGetComponent(Unity.Entities.Entity entity, out T component) { component = default; return false; } }
        ";

        [Fact]
        public async Task ExecuteMethodWithDestroyEntity_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    [ReadOnly] public ComponentLookup<HealthComponent> HealthComponentLookup;
                    [ReadOnly] public BufferLookup<DamageModule> DamageModulesLookup;
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in HitEventComponent hitEvent)
                    {
                        Ecb.DestroyEntity(index, eventEntity);
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ExecuteMethodWithDestroyEntityInCalledMethod_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    [ReadOnly] public ComponentLookup<HealthComponent> HealthComponentLookup;
                    [ReadOnly] public BufferLookup<DamageModule> DamageModulesLookup;
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in HitEventComponent hitEvent)
                    {
                        Cleanup(index, eventEntity);
                    }
                    private void Cleanup(in int index, in Unity.Entities.Entity eventEntity)
                    {
                        Ecb.DestroyEntity(index, eventEntity);
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ExecuteMethodWithoutDestroyEntity_TriggersWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    [ReadOnly] public ComponentLookup<HealthComponent> HealthComponentLookup;
                    [ReadOnly] public BufferLookup<DamageModule> DamageModulesLookup;
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in HitEventComponent {|#0:hitEvent|})
                    {
                        // any other code
                    }
                    private void Cleanup(in int index, in Unity.Entities.Entity eventEntity)
                    {
                        Ecb.DestroyEntity(index, eventEntity);
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceEntityRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("hitEvent");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task NonExecuteMethod_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public void SomeOtherMethod(in Unity.Entities.Entity eventEntity, in HitEventComponent hitEvent)
                    {
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ExecuteMethodInNonIJobEntityStruct_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public struct HitEventProcessorSystemJob
                {
                    public void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in HitEventComponent hitEvent)
                    {
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ExecuteMethodWithoutDestroyEntityInCalledMethod_TriggersWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    [ReadOnly] public ComponentLookup<HealthComponent> HealthComponentLookup;
                    [ReadOnly] public BufferLookup<DamageModule> DamageModulesLookup;
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in HitEventComponent {|#0:hitEvent|})
                    {
                    }
                    private void SomeMethod(in int index, in Unity.Entities.Entity eventEntity)
                    {
                        Ecb.DestroyEntity(index, eventEntity);
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceEntityRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("hitEvent");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task ComponentWithoutIEntityMustBeRemoved_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in HealthComponent health)
                    {
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }
        
        [Fact]
        public async Task ComponentWithIEntityMustBeRemoved_DoesTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    [ReadOnly] public ComponentLookup<HealthComponent> HealthComponentLookup;
                    [ReadOnly] public BufferLookup<DamageModule> DamageModulesLookup;
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;

                    private void Execute([ChunkIndexInQuery] in int index,
                        in Unity.Entities.Entity eventEntity,
                        in HitEventComponent {|#0:hitEvent|})
                    {
                        if (!HealthComponentLookup.TryGetComponent(hitEvent.Target, out var healthComponent))
                        {
                            return;
                        }
                        if (!DamageModulesLookup.TryGetBuffer(hitEvent.Owner, out var damageModules))
                        {
                            return;
                        }

                        Ecb.AddBuffer<HealthChangedEvent>(index, hitEvent.Target);
                        // foreach (var module in damageModules)
                        // {
                        //     Ecb.AppendToBuffer(index, hitEvent.Target, new HealthChangedEvent { Value = module.Value, Invoker = hitEvent.Owner});
                        // }
                    }

                    private void Cleanup(in int index,
                        in Unity.Entities.Entity eventEntity)
                    {
                        Ecb.DestroyEntity(index, eventEntity);
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceEntityRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("hitEvent");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task WithAllAttributeWithIEntityMustBeRemovedComponent_TriggersWarning()
        {
            var testCode = Stubs + @"
                [WithAll({|#0:typeof(HitEventComponent)|})]
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;

                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity)
                    {
                        // no DestroyEntity
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceEntityRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("HitEventComponent");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }
    }
}