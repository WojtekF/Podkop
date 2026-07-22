# Backend uses vertical slices with Clean Architecture layers per feature

Each backend feature (Findings, Votes, Comments, …) is a self-contained vertical slice under `Features/`, composed of four layer projects (`Podkop.<Feature>.Domain/Application/Infrastructure/Server`) with dependencies pointing inward; use cases are CQRS commands/queries dispatched through MediatR, and `Podkop.Server` remains the composition root that wires the slices together. We chose this over a single layered solution (one Domain/Application/Infrastructure for everything) because slices keep each feature's model small and independently evolvable, and over simple endpoint folders because the layer split keeps domain logic testable and free of persistence concerns.

## Consequences

- Features never reference each other's internals; cross-feature communication goes through contracts/events.
- A slice that publishes cross-feature events adds an optional fifth layer project, `Podkop.<Feature>.Contracts`, holding only public event records (MediatR `INotification`s carrying primitive facts). Domain events stay internal to the slice's Domain project; the Infrastructure layer translates them into contract events after persistence. Consuming slices reference only the producer's Contracts project.
