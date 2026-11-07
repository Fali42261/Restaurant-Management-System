# UI architecture

This project uses the Model-View-ViewModel (MVVM) pattern.

- **Models** contain application data structures.
- **Views** contain MAUI XAML pages and their code-behind.
- **ViewModels** expose bindable state and commands to Views.
- **Services/Interfaces** define application use cases and external dependencies.
- **Services/Implementations** contain concrete service implementations.
- **Repositories/Interfaces** define data-access contracts.
- **Repositories/Implementations** contain data-access implementations.
- **Constants**, **Helpers**, and **Converters** contain shared UI support code.

Keep business and data-access logic out of Views. Inject services into ViewModels through dependency injection in MauiProgram.cs.

The initial structure intentionally adds contracts and base types only. Concrete authentication, persistence, navigation, and API implementations should be added after the existing application flow and data layer are reviewed.
