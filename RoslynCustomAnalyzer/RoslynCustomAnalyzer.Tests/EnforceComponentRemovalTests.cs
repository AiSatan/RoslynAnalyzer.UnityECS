using System.Threading.Tasks;
using Xunit;
using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    RoslynCustomAnalyzer.EnforceComponentRemovalAnalyzer>;

namespace RoslynCustomAnalyzer.Tests
{
    public class EnforceComponentRemovalTests
    {
        // Common code stubs for Unity types to make the test code compile.
        private const string Stubs = @"
            using System;
            namespace Unity.Entities
            {
                public interface IComponentData {}
                public interface IComponentMustBeRemoved {}
                public struct Entity {}
                public partial interface IJobEntity {}
                public struct EntityCommandBuffer
                {
                    public struct ParallelWriter
                    {
                        public void DestroyEntity(int index, Entity entity) {}
                        public void RemoveComponent<T>(int index, Entity entity) {}
                        public void AddComponent<T>(int index, Entity entity) {}
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
            public struct TempComponent : Unity.Entities.IComponentData, Unity.Entities.IComponentMustBeRemoved {}
            public struct HealthComponent : Unity.Entities.IComponentData {}
            public struct DamageModule {}
            public struct BufferLookup<T> {}
            public struct ComponentLookup<T> {}
        ";

        [Fact]
        public async Task ExecuteMethodWithRemoveComponent_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in TempComponent temp)
                    {
                        Ecb.RemoveComponent<TempComponent>(index, entity);
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ExecuteMethodWithRemoveComponentInCalledMethod_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in TempComponent temp)
                    {
                        Cleanup(index, entity);
                    }
                    private void Cleanup(in int index, in Unity.Entities.Entity entity)
                    {
                        Ecb.RemoveComponent<TempComponent>(index, entity);
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ExecuteMethodWithoutRemoveComponent_TriggersWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in TempComponent {|#0:temp|})
                    {
                        // any other code
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceComponentRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("temp");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task RemoveComponentInAddComponentMethod_DoesNotCount()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in TempComponent {|#0:temp|})
                    {
                        AddTemp(index, entity);
                    }
                    private void AddTemp(in int index, in Unity.Entities.Entity entity)
                    {
                        Ecb.AddComponent<TempComponent>(index, entity);
                        Ecb.RemoveComponent<TempComponent>(index, entity);
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceComponentRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("temp");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task RemoveComponentInOtherMethod_WhenAddedInDifferentMethod_DoesCount()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in TempComponent temp)
                    {
                        AddTemp(index, entity);
                        RemoveTemp(index, entity);
                    }
                    private void AddTemp(in int index, in Unity.Entities.Entity entity)
                    {
                        Ecb.AddComponent<TempComponent>(index, entity);
                    }
                    private void RemoveTemp(in int index, in Unity.Entities.Entity entity)
                    {
                        Ecb.RemoveComponent<TempComponent>(index, entity);
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ComponentWithoutIComponentMustBeRemoved_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in HealthComponent health)
                    {
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task WithAllAttributeWithIComponentMustBeRemovedComponent_TriggersWarning()
        {
            var testCode = Stubs + @"
                [WithAll({|#0:typeof(TempComponent)|})]
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity)
                    {
                        // no RemoveComponent
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceComponentRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("TempComponent");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }
    }
}