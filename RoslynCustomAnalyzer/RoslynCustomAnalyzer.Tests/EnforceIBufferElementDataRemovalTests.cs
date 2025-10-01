using System.Threading.Tasks;
using Xunit;
using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    RoslynCustomAnalyzer.EnforceComponentRemovalAnalyzer>;

namespace RoslynCustomAnalyzer.Tests
{
    public class EnforceIBufferElementDataRemovalTests
    {
        private const string Stubs = @"
            using System;
            namespace Unity.Entities
            {
                public interface IComponentData {}
                public interface IBufferElementData {}
                public interface IComponentMustBeRemoved {}
                public interface IEntityMustBeRemoved {}
                public struct Entity {}
                public partial interface IJobEntity {}
                public struct DynamicBuffer<T> where T : struct {}
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
            public struct TempBufferElement : Unity.Entities.IBufferElementData, Unity.Entities.IComponentMustBeRemoved {}
            public struct HitEventComponent : Unity.Entities.IComponentData, Unity.Entities.IEntityMustBeRemoved { public Unity.Entities.Entity Target { get; } public Unity.Entities.Entity Owner { get; } }
            public struct HitEventBufferElement : Unity.Entities.IBufferElementData, Unity.Entities.IEntityMustBeRemoved { public Unity.Entities.Entity Target { get; } public Unity.Entities.Entity Owner { get; } }
        ";

        [Fact]
        public async Task BufferElementWithIComponentMustBeRemoved_TriggersWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in TempBufferElement {|#0:temp|})
                    {
                        // no RemoveComponent
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceComponentRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("temp");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task BufferElementWithIComponentMustBeRemoved_DoesNotTriggerWarning_WhenRemoved()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in TempBufferElement temp)
                    {
                        Ecb.RemoveComponent<TempBufferElement>(index, entity);
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task DynamicBufferWithIComponentMustBeRemoved_TriggersWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in Unity.Entities.DynamicBuffer<TempBufferElement> {|#0:tasks|})
                    {
                        // no RemoveComponent
                    }
                }";

            var expected = AnalyzerVerifier.Diagnostic(EnforceComponentRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("tasks");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task DynamicBufferWithIComponentMustBeRemoved_DoesNotTriggerWarning_WhenRemoved()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct TempComponentProcessorJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;
                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in Unity.Entities.DynamicBuffer<TempBufferElement> tasks)
                    {
                        Ecb.RemoveComponent<TempBufferElement>(index, entity);
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task BufferElementWithIEntityMustBeRemoved_TriggersWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;

                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in HitEventBufferElement {|#0:hitEvent|})
                    {
                        // no DestroyEntity
                    }
                }";

            var expected = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<EnforceEntityRemovalAnalyzer>.Diagnostic(EnforceEntityRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("hitEvent");

            await Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<EnforceEntityRemovalAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task BufferElementWithIEntityMustBeRemoved_DoesNotTriggerWarning_WhenDestroyed()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;

                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in HitEventBufferElement hitEvent)
                    {
                        Ecb.DestroyEntity(index, eventEntity);
                    }
                }";

            await Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<EnforceEntityRemovalAnalyzer>.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task DynamicBufferWithIEntityMustBeRemoved_TriggersWarning()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;

                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in Unity.Entities.DynamicBuffer<HitEventBufferElement> {|#0:hitEvents|})
                    {
                        // no DestroyEntity
                    }
                }";

            var expected = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<EnforceEntityRemovalAnalyzer>.Diagnostic(EnforceEntityRemovalAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("hitEvents");

            await Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<EnforceEntityRemovalAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task DynamicBufferWithIEntityMustBeRemoved_DoesNotTriggerWarning_WhenDestroyed()
        {
            var testCode = Stubs + @"
                [BurstCompile]
                public partial struct HitEventProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public Unity.Entities.EntityCommandBuffer.ParallelWriter Ecb;

                    private void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity eventEntity, in Unity.Entities.DynamicBuffer<HitEventBufferElement> hitEvents)
                    {
                        Ecb.DestroyEntity(index, eventEntity);
                    }
                }";

            await Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<EnforceEntityRemovalAnalyzer>.VerifyAnalyzerAsync(testCode);
        }
    }
}
