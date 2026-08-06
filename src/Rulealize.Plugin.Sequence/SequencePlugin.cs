// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Plugins;

namespace Rulealize.Plugin.Sequence
{
    /// <summary>
    /// Generation, transformation and aggregation over sequences, in the <c>seq</c>
    /// namespace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This plugin does not know where a sequence came from. Othello feeds it rays and
    /// coordinate lists produced by a grid plugin it has never heard of; what makes that
    /// work is that <c>Sequence</c> is a kind in the shared value model.
    /// </para>
    /// <para>
    /// It is also the most obvious candidate for replacement in the standard set. Eager
    /// versus lazy, buffered versus recomputed, sequential versus parallel — all of that is
    /// this implementation's business and nobody else's, so long as the re-enumerability
    /// requirement holds.
    /// </para>
    /// </remarks>
    public sealed class SequencePlugin : IRulealizePlugin
    {
        /// <inheritdoc />
        public PluginManifest Manifest { get; } =
            new("Rulealize.Plugin.Sequence", new Version(1, 0, 0), "seq");

        /// <inheritdoc />
        public void Register(IPluginRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            registry.AddExpression("empty", EmptyNode.Build);
            registry.AddExpression("any", AnyNode.Build);
            registry.AddExpression("count", CountNode.Build);
            registry.AddExpression("elementAt", ElementAtNode.Build);
            registry.AddExpression("takeWhile", TakeWhileNode.Build);
            registry.AddExpression("selectMany", SelectManyNode.Build);
            registry.AddExpression("where", WhereNode.Build);
            registry.AddExpression("select", SelectNode.Build);
        }
    }
}
