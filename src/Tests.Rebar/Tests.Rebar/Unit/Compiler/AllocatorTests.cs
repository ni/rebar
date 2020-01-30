﻿using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NationalInstruments.Compiler;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
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
            Constant constant = ConnectConstantToInputTerminal(inspect.InputTerminals[0], PFTypes.Int32, 5, false);

            Dictionary<VariableReference, ValueSource> valueSources = RunAllocator(function);

            ValueSource integerValueSource = valueSources[constant.OutputTerminal.GetTrueVariable()];
            Assert.IsInstanceOfType(integerValueSource, typeof(ConstantValueSource));
            ValueSource inspectInputValueSource = valueSources[inspect.InputTerminals[0].GetTrueVariable()];
            Assert.IsInstanceOfType(inspectInputValueSource, typeof(ReferenceToSingleValueSource));
        }

        [TestMethod]
        public void AddConstants_Allocate_SumGetsImmutableValueSource()
        {
            DfirRoot function = DfirRoot.Create();
            var add = new FunctionalNode(function.BlockDiagram, Signatures.DefinePureBinaryFunction("Add", PFTypes.Int32, PFTypes.Int32));
            ConnectConstantToInputTerminal(add.InputTerminals[0], PFTypes.Int32, false);
            ConnectConstantToInputTerminal(add.InputTerminals[1], PFTypes.Int32, false);

            Dictionary<VariableReference, ValueSource> valueSources = RunAllocator(function);

            ValueSource sumSource = valueSources[add.OutputTerminals[2].GetTrueVariable()];
            Assert.IsInstanceOfType(sumSource, typeof(ImmutableValueSource));
        }

        [TestMethod]
        public void AddConstantsAndYieldResult_Allocate_SumGetsLocalAllocation()
        {
            DfirRoot function = DfirRoot.Create();
            var add = new FunctionalNode(function.BlockDiagram, Signatures.DefinePureBinaryFunction("Add", PFTypes.Int32, PFTypes.Int32));
            ConnectConstantToInputTerminal(add.InputTerminals[0], PFTypes.Int32, false);
            ConnectConstantToInputTerminal(add.InputTerminals[1], PFTypes.Int32, false);
            var yieldNode = new FunctionalNode(function.BlockDiagram, Signatures.YieldType);
            Wire.Create(function.BlockDiagram, add.OutputTerminals[2], yieldNode.InputTerminals[0]);
            Dictionary<VariableReference, ValueSource> valueSources = RunAllocator(function);

            ValueSource sumSource = valueSources[add.OutputTerminals[2].GetTrueVariable()];
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
            Dictionary<VariableReference, ValueSource> valueSources = RunAllocator(function);

            ValueSource sumSource = valueSources[concat.OutputTerminals[2].GetTrueVariable()];
            Assert.IsInstanceOfType(sumSource, typeof(StateFieldValueSource));
        }

        private Dictionary<VariableReference, ValueSource> RunAllocator(DfirRoot function)
        {
            var cancellationToken = new CompileCancellationToken();
            RunCompilationUpToAutomaticNodeInsertion(function, cancellationToken);
            ExecutionOrderSortingVisitor.SortDiagrams(function);

            var asyncStateGrouper = new AsyncStateGrouper();
            asyncStateGrouper.Execute(function, cancellationToken);
            IEnumerable<AsyncStateGroup> asyncStateGroups = asyncStateGrouper.GetAsyncStateGroups();

            Dictionary<VariableReference, ValueSource> valueSources = VariableReference.CreateDictionaryWithUniqueVariableKeys<ValueSource>();
            var additionalSources = new Dictionary<object, ValueSource>();
            var allocator = new Allocator(valueSources, additionalSources, asyncStateGroups);
            allocator.Execute(function, cancellationToken);
            return valueSources;
        }
    }
}
