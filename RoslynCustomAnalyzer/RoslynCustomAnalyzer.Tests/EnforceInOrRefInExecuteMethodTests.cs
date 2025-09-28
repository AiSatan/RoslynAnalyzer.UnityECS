using System.Threading.Tasks;
using Xunit;
using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    RoslynCustomAnalyzer.EnforceInOrRefInExecuteMethodAnalyzer>;

namespace RoslynCustomAnalyzer.Tests
{
    public class EnforceInOrRefInExecuteMethodTests
    {
        // Common code stubs for Unity types to make the test code compile.
        private const string Stubs = @"
            using System;
            namespace Unity.Entities
            {
                public interface IComponentData {}
                public struct Entity {}
                public partial interface IJobEntity {}
            }
            public class ChunkIndexInQuery : System.Attribute {}
            public struct EasyJointAutoScaleRequired : Unity.Entities.IComponentData {}
            public struct EasyJointAnchor : Unity.Entities.IComponentData {}
            public struct EasyJointTarget : Unity.Entities.IComponentData {}
            public struct PostTransformMatrix : Unity.Entities.IComponentData {}
            public struct CharacterAspect {}
        ";

        [Fact]
        public async Task ExecuteMethodWithoutInOrRef_TriggersWarning()
        {
            var testCode = Stubs + @"
                public partial struct EasyJointScaleProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public void Execute(Unity.Entities.Entity {|#0:entity|}, EasyJointAutoScaleRequired {|#1:baseScale|}, EasyJointAnchor {|#2:anchor|}, EasyJointTarget {|#3:target|}, PostTransformMatrix {|#4:postTransformMatrix|}, CharacterAspect characterAspect)
                    {
                    }
                }";

            var expected1 = AnalyzerVerifier.Diagnostic(EnforceInOrRefInExecuteMethodAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("entity");

            var expected2 = AnalyzerVerifier.Diagnostic(EnforceInOrRefInExecuteMethodAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("baseScale");

            var expected3 = AnalyzerVerifier.Diagnostic(EnforceInOrRefInExecuteMethodAnalyzer.DiagnosticId)
                .WithLocation(2)
                .WithArguments("anchor");

            var expected4 = AnalyzerVerifier.Diagnostic(EnforceInOrRefInExecuteMethodAnalyzer.DiagnosticId)
                .WithLocation(3)
                .WithArguments("target");

            var expected5 = AnalyzerVerifier.Diagnostic(EnforceInOrRefInExecuteMethodAnalyzer.DiagnosticId)
                .WithLocation(4)
                .WithArguments("postTransformMatrix");

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode, expected1, expected2, expected3, expected4, expected5);
        }

        [Fact]
        public async Task ExecuteMethodWithInOrRef_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                public partial struct EasyJointScaleProcessorSystemJob : Unity.Entities.IJobEntity
                {
                    public void Execute([ChunkIndexInQuery] in int index, in Unity.Entities.Entity entity, in EasyJointAutoScaleRequired baseScale, in EasyJointAnchor anchor, in EasyJointTarget target, ref PostTransformMatrix postTransformMatrix, CharacterAspect characterAspect)
                    {
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task NonExecuteMethod_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                public partial struct MyStruct : Unity.Entities.IJobEntity
                {
                    public void SomeOtherMethod(Unity.Entities.Entity entity)
                    {
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ExecuteMethodInNonIJobEntityStruct_DoesNotTriggerWarning()
        {
            var testCode = Stubs + @"
                public struct MyStruct
                {
                    public void Execute(Unity.Entities.Entity entity)
                    {
                    }
                }";

            await AnalyzerVerifier.VerifyAnalyzerAsync(testCode);
        }
    }
}