﻿using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NationalInstruments;
using NationalInstruments.Compiler;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using NationalInstruments.ExecutionFramework;
using Rebar.Common;
using Rebar.Compiler;
using Rebar.Compiler.Nodes;
using Rebar.RebarTarget;
using Rebar.RebarTarget.LLVM;
using Tests.Rebar.Unit.Execution;

namespace Tests.Rebar.Unit.Compiler
{
    [TestClass]
    public class AllocatorTests : ExecutionTestBase
    {
        [TestMethod]
        public void IntegerConstantIntoInspect_Allocate_IntegerVariableGetsConstantValueSource()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode inspect = new FunctionalNode(function.BlockDiagram, Signatures.InspectType);
            Constant constant = ConnectConstantToInputTerminal(inspect.InputTerminals[0], NITypes.Int32, 5, false);

            FunctionVariableStorage valueStorage = RunAllocator(function);

            ValueSource integerValueSource = valueStorage.GetValueSourceForVariable(constant.OutputTerminal.GetTrueVariable());
            Assert.IsInstanceOfType(integerValueSource, typeof(ImmutableValueSource));
            ValueSource inspectInputValueSource = valueStorage.GetValueSourceForVariable(inspect.InputTerminals[0].GetTrueVariable());
            Assert.IsInstanceOfType(inspectInputValueSource, typeof(ReferenceToSingleValueSource));
        }

        [TestMethod]
        public void AddConstants_Allocate_SumGetsImmutableValueSource()
        {
            DfirRoot function = DfirRoot.Create();
            var add = new FunctionalNode(function.BlockDiagram, Signatures.DefinePureBinaryFunction("Add", NITypes.Int32, NITypes.Int32));
            ConnectConstantToInputTerminal(add.InputTerminals[0], NITypes.Int32, false);
            ConnectConstantToInputTerminal(add.InputTerminals[1], NITypes.Int32, false);

            FunctionVariableStorage valueStorage = RunAllocator(function);

            ValueSource sumSource = valueStorage.GetValueSourceForVariable(add.OutputTerminals[2].GetTrueVariable());
            Assert.IsInstanceOfType(sumSource, typeof(ImmutableValueSource));
        }

        [TestMethod]
        public void AddConstantsAndYieldResult_Allocate_SumGetsLocalAllocation()
        {
            DfirRoot function = DfirRoot.Create();
            var add = new FunctionalNode(function.BlockDiagram, Signatures.DefinePureBinaryFunction("Add", NITypes.Int32, NITypes.Int32));
            ConnectConstantToInputTerminal(add.InputTerminals[0], NITypes.Int32, false);
            ConnectConstantToInputTerminal(add.InputTerminals[1], NITypes.Int32, false);
            var yieldNode = new FunctionalNode(function.BlockDiagram, Signatures.YieldType);
            Wire.Create(function.BlockDiagram, add.OutputTerminals[2], yieldNode.InputTerminals[0]);

            FunctionVariableStorage valueStorage = RunAllocator(function);

            ValueSource sumSource = valueStorage.GetValueSourceForVariable(add.OutputTerminals[2].GetTrueVariable());
            Assert.IsInstanceOfType(sumSource, typeof(LocalAllocationValueSource));
        }

        [TestMethod]
        public void ConcatenateStringsAndYieldResult_Allocate_ConcatenatedStringGetsStateField()
        {
            DfirRoot function = DfirRoot.Create();
            var concat = new FunctionalNode(function.BlockDiagram, Signatures.StringConcatType);
            ConnectConstantToInputTerminal(concat.InputTerminals[0], DataTypes.StringSliceType.CreateImmutableReference(), false);
            ConnectConstantToInputTerminal(concat.InputTerminals[1], DataTypes.StringSliceType.CreateImmutableReference(), false);
            var yieldNode = new FunctionalNode(function.BlockDiagram, Signatures.YieldType);
            Wire.Create(function.BlockDiagram, concat.OutputTerminals[2], yieldNode.InputTerminals[0]);

            FunctionVariableStorage valueStorage = RunAllocator(function);

            ValueSource sumSource = valueStorage.GetValueSourceForVariable(concat.OutputTerminals[2].GetTrueVariable());
            Assert.IsInstanceOfType(sumSource, typeof(StateFieldValueSource));
        }

        private FunctionVariableStorage RunAllocator(DfirRoot function)
        {
            var cancellationToken = new CompileCancellationToken();
            RunCompilationUpToAsyncNodeDecomposition(function, cancellationToken);
            ExecutionOrderSortingVisitor.SortDiagrams(function);

            var asyncStateGrouper = new AsyncStateGrouper();
            asyncStateGrouper.Execute(function, cancellationToken);
            IEnumerable<AsyncStateGroup> asyncStateGroups = asyncStateGrouper.GetAsyncStateGroups();

            using (var contextWrapper = new ContextWrapper())
            {
                var module = contextWrapper.CreateModule("module");
                var functionImporter = new FunctionImporter(contextWrapper, module);
                var codeGenExpander = new CodeGenExpander(
                    function,
                    new FunctionModuleContext(contextWrapper, module, functionImporter),
                    new Dictionary<CompilableDefinitionName, bool>());
                asyncStateGroups.ForEach(codeGenExpander.ExpandAsyncStateGroup);

                var variableStorage = new FunctionVariableStorage();
                var allocator = new Allocator(contextWrapper, variableStorage, asyncStateGroups);
                allocator.Execute(function, cancellationToken);
                return variableStorage;
            }
        }
    }
}
