# Sedgewright Agent Notes

This is a cross-platform Avalonia UI app built as a single `Sedgewright` project. Keep UI in Views and ViewModels, keep app logic inside the project folders, and avoid Windows-only APIs.

The app also uses the GitHub Copilot SDK through `Sedgewright`. Prefer async/await, deterministic behavior, and small structured chat responses. Keep guidance concise and developer-focused.

# Rules

## Model for sub-agent
- All sub-agents models should default to using the same model as the parent agent unless explicitly overridden.

## For Avalonia UI
- Always keep in mind that this is a cross-platform app. Avoid Windows-specific APIs and patterns.
- Use MVVM and XAML for UI. Keep code-behind minimal.
- The UI has light and dark modes. Any change to the UI must support both themes.
- Prefer compact, borderless, minimal controls with small text.

## Project Structure
- `Sedgewright` is the only app project. Inside it:
- `Views` folder contains Avalonia windows and controls.
- `ViewModels` folder contains binding state, commands, and UI coordination.
- `Apps` folder contains application workflows, orchestration, and use-case services.
- `Models` folder contains shared data models, settings snapshots, message contracts, and other app state structures.
- `Infras` folder contains persistence, SDK integration, storage, and other concrete adapters.
- Keep shared logic out of Views and code-behind; prefer ViewModels plus the `Apps`, `Models`, and `Infras` folders for reusable responsibilities.

## For migrations
- Only create migrations by running this command in the `Sedgewright` project folder, never try to generate migration files manually.
```bash
dotnet ef migrations add <MigrationName>
```

## Shared State
- Use `ISharedState` (in `Apps/Abstractions`, implemented by `SharedState` in `Infras/Services`) to share data that must stay synchronized across unrelated views/ViewModels, instead of notifying views directly or re-querying storage from multiple places.
- `SharedState` is registered as a singleton in DI; inject `ISharedState` into any ViewModel that needs to read or refresh shared data (see `ChatViewModel` and `OrchestrationDefinitionsViewModel`).
- Expose shared collections as `ObservableCollection<T>` so all bound views update automatically; mutate/replace the collection in place rather than reassigning the reference.
- Provide an explicit async refresh method (e.g. `RefreshOrchestrationsAsync`) that reloads from the repository/database and marshals collection updates onto the UI thread via `Dispatcher.UIThread`.
- Treat shared state as the UI source of truth: update shared state first for responsiveness, then persist through repositories; only reach for a full database round-trip before updating state when strict consistency is required.

## Main View
- The main view is the app shell and the entry point for the UI.
- It hosts four primary areas:
  - Chat: the main working surface for sending prompts, selecting an agent and model, and viewing streamed responses.
  - Settings: app-wide configuration such as theme, default model, working directory, and predefined agent configuration.
  - Skills: discovered skills available to the app.
  - Agents: custom agents, including their prompt, tools, and skills.
- Keep the shell focused on composition and navigation; put behavior in the appropriate folder or ViewModel.

## Coding Convention
- Use primary constructors and sealed classes whenever possible for classes and records.
- Use extension members of C# 14 for extension methods and properties.
  ```csharp
  public static class StringExtensions
  {
      extension(string s)
      {
          public bool IsNullOrWhiteSpace =>
              string.IsNullOrWhiteSpace(s);

          public string Repeat(int count) =>
              string.Concat(Enumerable.Repeat(s, count));
      }
  }
  ```
- Use collection expressions whenever possible when working with collections.
  ```csharp
  int[] squares = [1, 4, 9, 16];
  ```
- Add comprehensive comments for newly created public classes and methods.
- Prefer explicit typing over `var`. For example, use `string name = "Sedgewright";` instead of `var name = "Sedgewright"`.

## CSharpFunctionalExtensions
- Use `Result<T, IApplicationError>` for data-bearing operations and `UnitResult<IApplicationError>` for commands or validations that only need success/failure.
- Keep expected failures inside results and model them with the repo's error records such as `ValidationError`, `GenericError`, and `InvalidOperationError`; reserve `ExceptionError` for unexpected exceptions.
- Prefer railway-oriented chains with `Bind`, `Map`, `Ensure`, and `Tap` so each step stays explicit and short-circuits automatically on failure.
- `Ensure` checks the current success value and turns a failed business rule into a failure.
- `Bind` is for the next step when that step also returns a `Result` or `UnitResult`.
- `Map` is for pure value conversion on success only.
- `Tap` is for side effects on success only, such as saving, logging, or publishing.
- Use `ToResult` when turning optional or missing values into failures, and use `Result.Combine` when multiple validations must all succeed before construction.
- Avoid `.Value` unless success is already guaranteed; if a result is going to be consumed immediately, keep it in the pipeline and map it to the final shape instead of unpacking it early.

  ```csharp
  return FindCustomer(id)
      .ToResult("Customer not found")
      .Ensure(customer => customer.IsActive, "Customer is inactive")
      .Bind(customer => PromoteCustomer(customer))
      .Tap(customer => _customerRepository.Save(customer))
      .Map(customer => new CustomerDto(customer.Id, customer.Name));
  ```

## Optional Reference Docs
- Read [Avalonia](../doc/Avalonia/README.md) and the linked files in `doc\Avalonia\` only when you need broader Avalonia framework context.
- Read [Avalonia performance best practices](../doc/Avalonia/07-performance-best-practices.md) when you need optimization guidance for layouts, bindings, virtualization, rendering, or UI responsiveness.
