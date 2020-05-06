﻿using System.Xml.Linq;
using NationalInstruments.Core;
using NationalInstruments.DynamicProperties;
using NationalInstruments.SourceModel;
using NationalInstruments.SourceModel.Persistence;
using NationalInstruments.VI.SourceModel;
using Rebar.Common;

namespace Rebar.SourceModel
{
    public class BorrowTunnel : FlatSequenceTunnel, IBeginLifetimeTunnel, IBorrowTunnel
    {
        private const string ElementName = "FlatSequenceBorrowTunnel";

        [XmlParserFactoryMethod(ElementName, Function.ParsableNamespaceName)]
        public static BorrowTunnel CreateBorrowTunnel(IElementCreateInfo elementCreateInfo)
        {
            var borrowTunnel = new BorrowTunnel();
            borrowTunnel.Initialize(elementCreateInfo);
            return borrowTunnel;
        }

        /// <inheritdoc />
        public override XName XmlElementName => XName.Get(ElementName, Function.ParsableNamespaceName);

        public static readonly PropertySymbol TerminateLifetimeTunnelPropertySymbol =
            ExposeIdReferenceProperty<BorrowTunnel>(
                "TerminateLifetimeTunnel",
                borrowTunnel => borrowTunnel.TerminateLifetimeTunnel,
                (borrowTunnel, terminateLifetimeTunnel) => borrowTunnel.TerminateLifetimeTunnel = (FlatSequenceTerminateLifetimeTunnel)terminateLifetimeTunnel);

        public static readonly PropertySymbol BorrowModePropertySymbol =
            ExposeStaticProperty<BorrowTunnel>(
                "BorrowMode",
                borrowTunnel => borrowTunnel.BorrowMode,
                (borrowTunnel, value) => borrowTunnel.BorrowMode = (BorrowMode)value,
                PropertySerializers.CreateEnumSerializer<BorrowMode>(),
                BorrowMode.Immutable
            );

        private BorrowMode _borrowMode;

        public BorrowTunnel()
        {
            Docking = BorderNodeDocking.Left;
            _borrowMode = BorrowMode.Immutable;
        }

        public override BorderNodeRelationship Relationship => BorderNodeRelationship.AncestorToDescendant;

        // TODO: this will not be the case for BorrowTunnels on case structures
        public override BorderNodeMultiplicity Multiplicity => BorderNodeMultiplicity.OneToOne;

        public ITerminateLifetimeTunnel TerminateLifetimeTunnel { get; set; }

        public BorrowMode BorrowMode
        {
            get { return _borrowMode; }
            set
            {
                if (_borrowMode != value)
                {
                    TransactionRecruiter.EnlistPropertyItem(
                        this, 
                        "BorrowMode", 
                        _borrowMode, 
                        value, 
                        (mode, reason) => _borrowMode = mode, 
                        TransactionHints.Semantic);
                    _borrowMode = value;
                }
            }
        }

        /// <inheritdoc />
        public override void EnsureView(EnsureViewHints hints)
        {
            EnsureViewWork(hints, new RectDifference());
        }

        /// <inheritdoc />
        public override void EnsureViewDirectional(EnsureViewHints hints, RectDifference oldBoundsMinusNewBounds)
        {
            EnsureViewWork(hints, oldBoundsMinusNewBounds);
        }

        private void EnsureViewWork(EnsureViewHints hints, RectDifference oldBoundsMinusNewbounds)
        {
            Docking = BorderNodeDocking.Left;
            base.EnsureViewDirectional(hints, oldBoundsMinusNewbounds);
        }
    }
}
