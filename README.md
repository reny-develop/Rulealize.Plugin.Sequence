# Rulealize.Plugin.Sequence

Generation, transformation and aggregation over
[Rulealize](https://github.com/reny-develop/Rulealize) sequences.

| | |
| --- | --- |
| Plugin id | `Rulealize.Plugin.Sequence` |
| Namespace | `seq` |
| Reserved prefix | none |
| Depends on | `Rulealize.Abstraction` |
| Specification | [doc/specification.md](doc/specification.md) |

`seq.empty`, `of`, `any`, `count`, `elementAt`, `takeWhile`, `where`, `select`,
`selectMany`.

This plugin does not know where a sequence came from. Reversi feeds it rays and coordinate
lists produced by a grid plugin it has never heard of, and that works because `Sequence` is
a kind in the shared value model — not because either plugin references the other.

It also introduces bindings without having any vocabulary for reading one: the name given
by `as` is read by `bind.local` and its `@` shorthand, in a plugin this one does not
reference. The scope machinery belongs to `Rulealize.Abstraction`, which both depend on.

**Sequences must be re-enumerable**, and that is the requirement to know before reading the
specification. Enumerating one sequence value twice must yield the same elements; laziness
is fine, a single-use iterator is not. Reversi binds a ray once and consumes it from two
places — `seq.takeWhile` and then `seq.elementAt` — so a single-use implementation would
silently return nothing the second time and the capture rule would quietly stop working.
Recompute on each enumeration or buffer on the first; either is fine.

That requirement is also what makes this the most obvious candidate for replacement in the
standard set. Eager or lazy, buffered or recomputed, sequential or parallel: all of it is
this implementation's business, so long as re-enumerability and sequence order hold.

## Building

`dotnet build`. `Rulealize.Abstraction` restores from nuget.org like any other package, so
this repository builds on its own.

## License

Apache-2.0.
