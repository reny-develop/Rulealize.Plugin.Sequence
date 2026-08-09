# Rulealize.Plugin.Sequence

Generation, transformation and aggregation over
[Rulealize](https://github.com/reny-develop/Rulealize) sequences.

| | |
| --- | --- |
| Plugin id | `Rulealize.Plugin.Sequence` |
| Namespace | `seq` |
| Reserved prefix | none |
| Depends on | `Rulealize.Abstraction` |

This plugin does not know where a sequence came from. Othello feeds it rays and coordinate
lists produced by a grid plugin it has never heard of, and that works because `Sequence` is
a kind in the shared value model — not because either plugin references the other.

It is also the most obvious candidate for replacement in the standard set. Eager or lazy,
buffered or recomputed, sequential or parallel: all of that is this implementation's
business, so long as the re-enumerability requirement below holds.

## Operations

| Operation | Shape |
| --- | --- |
| `seq.empty` | `{ "op": "seq.empty" }` |
| `seq.of` | `{ "op": "seq.of", "of": [ … ] }` |
| `seq.any` | `{ "op": "seq.any", "source": …, "as": "c", "predicate": … }` — both optional |
| `seq.count` | `{ "op": "seq.count", "source": …, "as": "c", "where": … }` — both optional |
| `seq.elementAt` | `{ "op": "seq.elementAt", "source": …, "index": … }` |
| `seq.takeWhile` | `{ "op": "seq.takeWhile", "source": …, "as": "c", "predicate": … }` |
| `seq.where` | `{ "op": "seq.where", "source": …, "as": "c", "predicate": … }` |
| `seq.select` | `{ "op": "seq.select", "source": …, "as": "c", "select": … }` |
| `seq.selectMany` | `{ "op": "seq.selectMany", "source": …, "as": "c", "select": … }` |

`seq.of` is the only way to write a sequence down. The value model gives sequences no JSON
literal, so before it existed every sequence in a rule set had to come from somewhere else —
Othello never noticed, because every sequence it uses comes out of a grid, but it left this
plugin able to produce nothing except emptiness. A knight's eight offsets are not a set any
grid operation has a name for; a rule set has to be able to say them.

The name given by `as` is visible only inside that node's predicate or projection, and
shadows an outer binding of the same name. It may be left out when the body does not look
at the element.

This plugin introduces bindings but has no vocabulary for reading one — that is
`bind.local`, and the `@` shorthand, in a plugin this one does not reference. The scope
machinery belongs to the runtime, and both reach it through `Rulealize.Abstraction`.

## Sequences must be re-enumerable

Enumerating the same sequence value twice must yield the same elements. Laziness is fine;
a single-use iterator is not.

This is a requirement, not an aspiration, and Othello demonstrates why. It binds a ray
once and consumes it from two places:

```jsonc
"bind": {
  "ray": { "op": "grid.ray", … },
  "run": { "op": "seq.takeWhile", "source": "@ray", … }     // first pass
},
"in": {
  …
  "coord": { "op": "seq.elementAt", "source": "@ray", … }   // second pass
}
```

`@ray` is evaluated once; the sequence it produced is walked twice. A single-use
implementation would silently return nothing the second time and the capture rule would
quietly stop working. Recompute on each enumeration or buffer on the first — either is
fine.

Sequences are also finite. Nothing here produces an unbounded one, because
`GetValidInputs` walks sequences to the end.

Elements need not share a kind. Othello alone produces sequences of coordinates, of
directions, and of cell values.

## Reading past the end is not an error

`seq.elementAt` returns null for an index beyond the last element. This is the start of the
null chain the value model specifies:

```
seq.elementAt(out of range) → null → grid.at(null) → null → cmp.eq(null, "black") → false
```

Which is what lets Othello's capture rule read one square past the run of opposing stones
without first checking that there is a square there.

A negative or fractional index is a different matter, and does fault. That is not pointing
outside the sequence; it is not an index.

## Order and short-circuiting

Elements are processed in sequence order, and the operations that stop early — `seq.any`
and `seq.takeWhile` — depend on it. A parallel implementation would still have to produce
what a sequential one produces.

`seq.any`'s short-circuit does real work. Othello asks whether the player to move has any
legal move in order to decide whether passing is allowed; the answer is usually yes and
usually found early. Without short-circuiting, every pass check would walk sixty-four
squares and eight rays out of each.

## Building

`Rulealize.Abstraction` is not on nuget.org yet, so `NuGet.config` points at a folder
feed. Produce it from the abstraction repository first:

```
dotnet pack path\to\Rulealize.Abstraction\src\Rulealize.Abstraction -c Release -o path\to\LocalNuGet
```

with `LocalNuGet` a sibling of this repository. Then `dotnet build`.

## License

Apache-2.0.
