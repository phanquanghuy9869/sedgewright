using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sedgewright.Apps.Abstractions;
using Sedgewright.Entities;
using Sedgewright.Infras.MAF.Extensions;
using Sedgewright.Models;
using Sedgewright.ViewModels.Shared;

namespace Sedgewright.ViewModels.Mcp;

/// <summary>
/// Drives the minimal MCP editor dialog.
/// </summary>
public sealed class McpDefinitionEditorViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions PrettyPrintOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Func<string, int?, Task<bool>> _nameExistsAsync;
    private readonly IMcpDefinitionRepository _repository;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _useFormCommand;
    private readonly RelayCommand _useJsonCommand;
    private readonly int? _existingId;
    private bool _isSaving;
    private bool _isStdioTransport = true;
    private bool _isHttpTransport;
    private bool _isFormConfigMode = true;
    private bool _isJsonConfigMode;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _isReadOnly;
    private string _stdioCommand = string.Empty;
    private string _stdioArgumentsText = string.Empty;
    private string _httpEndpoint = string.Empty;
    private string _httpHeadersText = string.Empty;
    private string _jsonConfigText = string.Empty;
    private string _validationText = string.Empty;

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event Action<UnitResult<IApplicationError>>? CloseRequested;

    /// <summary>
    /// Creates a new editor view-model.
    /// </summary>
    public McpDefinitionEditorViewModel(
        McpDefinition? definition,
        IMcpDefinitionRepository repository,
        Func<string, int?, Task<bool>> nameExistsAsync)
    {
        _nameExistsAsync = nameExistsAsync;
        _repository = repository;
        _existingId = definition?.Id;

        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _cancelCommand = new RelayCommand(Cancel);
        _useFormCommand = new RelayCommand(UseFormConfigMode);
        _useJsonCommand = new RelayCommand(UseJsonConfigMode);

        if (definition is null)
        {
            WindowTitle = "Add MCP definition";
            SubtitleText = "Use the form for quick edits, or switch to Json for exact control.";
            JsonConfigText = CreateJsonFromForm();
            return;
        }

        WindowTitle = $"Edit {definition.Name}";
        SubtitleText = "Keep the surface minimal. Switch to Json when you need the raw config.";
        Name = definition.Name;
        Description = definition.Description;
        IsReadOnly = definition.IsReadOnly;
        IsStdioTransport = definition.IsStdio;
        IsHttpTransport = !definition.IsStdio;

        if (!TryLoadFromJson(definition.ConfigJson))
        {
            _isFormConfigMode = false;
            _isJsonConfigMode = true;
            JsonConfigText = definition.ConfigJson;
        }
        else
        {
            JsonConfigText = CreateJsonFromForm();
        }
    }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string WindowTitle { get; }

    /// <summary>
    /// Gets the subtitle text.
    /// </summary>
    public string SubtitleText { get; }

    /// <summary>
    /// Gets or sets the MCP name.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the MCP description.
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            if (!SetProperty(ref _description, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets whether the MCP is read only.
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (!SetProperty(ref _isReadOnly, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the stdio transport toggle.
    /// </summary>
    public bool IsStdioTransport
    {
        get => _isStdioTransport;
        set
        {
            if (!SetProperty(ref _isStdioTransport, value))
            {
                return;
            }

            if (value && _isHttpTransport)
            {
                _isHttpTransport = false;
                OnPropertyChanged(nameof(IsHttpTransport));
            }

            OnTransportChanged();
        }
    }

    /// <summary>
    /// Gets or sets the HTTP/SSE transport toggle.
    /// </summary>
    public bool IsHttpTransport
    {
        get => _isHttpTransport;
        set
        {
            if (!SetProperty(ref _isHttpTransport, value))
            {
                return;
            }

            if (value && _isStdioTransport)
            {
                _isStdioTransport = false;
                OnPropertyChanged(nameof(IsStdioTransport));
            }

            OnTransportChanged();
        }
    }

    /// <summary>
    /// Gets or sets the stdio command.
    /// </summary>
    public string StdioCommand
    {
        get => _stdioCommand;
        set
        {
            if (!SetProperty(ref _stdioCommand, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the stdio arguments text.
    /// </summary>
    public string StdioArgumentsText
    {
        get => _stdioArgumentsText;
        set
        {
            if (!SetProperty(ref _stdioArgumentsText, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the HTTP endpoint.
    /// </summary>
    public string HttpEndpoint
    {
        get => _httpEndpoint;
        set
        {
            if (!SetProperty(ref _httpEndpoint, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the HTTP headers text.
    /// </summary>
    public string HttpHeadersText
    {
        get => _httpHeadersText;
        set
        {
            if (!SetProperty(ref _httpHeadersText, value))
            {
                return;
            }

            OnEditorFieldChanged();
        }
    }

    /// <summary>
    /// Gets or sets the raw Json config text.
    /// </summary>
    public string JsonConfigText
    {
        get => _jsonConfigText;
        set
        {
            if (!SetProperty(ref _jsonConfigText, value))
            {
                return;
            }

            if (IsJsonConfigMode)
            {
                _saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the form-mode toggle.
    /// </summary>
    public bool IsFormConfigMode
    {
        get => _isFormConfigMode;
        set
        {
            if (!SetProperty(ref _isFormConfigMode, value))
            {
                return;
            }

            if (value)
            {
                _isJsonConfigMode = false;
                OnPropertyChanged(nameof(IsJsonConfigMode));
                JsonConfigText = CreateJsonFromForm();
            }

            OnModeChanged();
        }
    }

    /// <summary>
    /// Gets or sets the Json-mode toggle.
    /// </summary>
    public bool IsJsonConfigMode
    {
        get => _isJsonConfigMode;
        set
        {
            if (!SetProperty(ref _isJsonConfigMode, value))
            {
                return;
            }

            if (value)
            {
                _isFormConfigMode = false;
                OnPropertyChanged(nameof(IsFormConfigMode));
                JsonConfigText = CreateJsonFromForm();
            }

            OnModeChanged();
        }
    }

    /// <summary>
    /// Gets the validation message.
    /// </summary>
    public string ValidationText
    {
        get => _validationText;
        private set
        {
            if (!SetProperty(ref _validationText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasValidationText));
        }
    }

    /// <summary>
    /// Gets whether the validation message is visible.
    /// </summary>
    public bool HasValidationText => !string.IsNullOrWhiteSpace(ValidationText);

    /// <summary>
    /// Gets the save command.
    /// </summary>
    public ICommand SaveCommand => _saveCommand;

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    public ICommand CancelCommand => _cancelCommand;

    /// <summary>
    /// Gets the form mode command.
    /// </summary>
    public ICommand UseFormCommand => _useFormCommand;

    /// <summary>
    /// Gets the Json mode command.
    /// </summary>
    public ICommand UseJsonCommand => _useJsonCommand;

    private bool CanSave()
        => !_isSaving && !string.IsNullOrWhiteSpace(Name);

    private async Task SaveAsync()
    {
        _isSaving = true;
        _saveCommand.RaiseCanExecuteChanged();

        string normalizedName = Normalize(Name);
        try
        {
            if (normalizedName.Length == 0)
            {
                ValidationText = "MCP name is required.";
                return;
            }

            if (await _nameExistsAsync(normalizedName, _existingId))
            {
                ValidationText = $"An MCP named '{normalizedName}' already exists.";
                return;
            }

            if (!TryBuildDefinition(out McpDefinition? definition, out string validationMessage))
            {
                ValidationText = validationMessage;
                return;
            }

            if (definition is null)
            {
                ValidationText = "Could not build the MCP definition.";
                return;
            }

            ValidationText = string.Empty;

            if (definition.Id == 0)
            {
                _repository.Create(definition);
            }
            else
            {
                _repository.Update(definition);
            }

            await _repository.SaveChangesAsync();

            CloseRequested?.Invoke(UnitResult.Success<IApplicationError>());
            return;
        }
        catch (DbUpdateException exception)
        {
            ValidationText = $"Could not save the MCP definition: {exception.Message}";
        }
        finally
        {
            _isSaving = false;
            _saveCommand.RaiseCanExecuteChanged();
        }
    }

    private void Cancel()
        => CloseRequested?.Invoke(UnitResult.Failure<IApplicationError>(new InvalidOperationError(["The editor was cancelled."])));

    private void UseFormConfigMode()
        => IsFormConfigMode = true;

    private void UseJsonConfigMode()
        => IsJsonConfigMode = true;

    private void OnEditorFieldChanged()
    {
        ValidationText = string.Empty;
        if (IsFormConfigMode)
        {
            JsonConfigText = CreateJsonFromForm();
        }

        _saveCommand.RaiseCanExecuteChanged();
    }

    private void OnTransportChanged()
    {
        ValidationText = string.Empty;
        OnPropertyChanged(nameof(IsStdioFieldsVisible));
        OnPropertyChanged(nameof(IsHttpFieldsVisible));
        if (IsFormConfigMode)
        {
            JsonConfigText = CreateJsonFromForm();
        }

        _saveCommand.RaiseCanExecuteChanged();
    }

    private void OnModeChanged()
    {
        OnPropertyChanged(nameof(IsFormConfigMode));
        OnPropertyChanged(nameof(IsJsonConfigMode));
        OnPropertyChanged(nameof(IsStdioFieldsVisible));
        OnPropertyChanged(nameof(IsHttpFieldsVisible));
        _saveCommand.RaiseCanExecuteChanged();
    }

    public bool IsStdioFieldsVisible => IsStdioTransport && IsFormConfigMode;

    public bool IsHttpFieldsVisible => IsHttpTransport && IsFormConfigMode;

    private bool TryBuildDefinition(out McpDefinition? definition, out string validationMessage)
    {
        definition = null;
        validationMessage = string.Empty;

        if (IsJsonConfigMode)
        {
            if (IsStdioTransport)
            {
                if (!TryParseJsonConfig<StdioConfig>(JsonConfigText, out StdioConfig? config, out validationMessage) || config is null)
                {
                    return false;
                }

                definition = CreateDefinition(config.Name, true, JsonSerializer.Serialize(config, PrettyPrintOptions));
                return true;
            }

            if (!TryParseJsonConfig<HttpTransportConfig>(JsonConfigText, out HttpTransportConfig? httpConfig, out validationMessage) || httpConfig is null)
            {
                return false;
            }

            definition = CreateDefinition(httpConfig.Name, false, JsonSerializer.Serialize(httpConfig, PrettyPrintOptions));
            return true;
        }

        if (IsStdioTransport)
        {
            string command = Normalize(StdioCommand);
            if (command.Length == 0)
            {
                validationMessage = "The stdio command is required.";
                return false;
            }

            StdioConfig config = new(Normalize(Name), command, ParseArguments(StdioArgumentsText));
            definition = CreateDefinition(config.Name, true, JsonSerializer.Serialize(config, PrettyPrintOptions));
            return true;
        }

        string endpoint = Normalize(HttpEndpoint);
        if (endpoint.Length == 0)
        {
            validationMessage = "The HTTP/SSE endpoint is required.";
            return false;
        }

        HttpTransportConfig httpTransportConfig = new(Normalize(Name), endpoint, ParseHeaders(HttpHeadersText));
        definition = CreateDefinition(httpTransportConfig.Name, false, JsonSerializer.Serialize(httpTransportConfig, PrettyPrintOptions));
        return true;
    }

    private bool TryLoadFromJson(string jsonConfig)
    {
        if (IsStdioTransport && TryParseJsonConfig<StdioConfig>(jsonConfig, out StdioConfig? stdioConfig, out _))
        {
            Name = stdioConfig!.Name;
            StdioCommand = stdioConfig!.Command;
            StdioArgumentsText = string.Join(Environment.NewLine, stdioConfig.Arguments);
            return true;
        }

        if (IsHttpTransport && TryParseJsonConfig<HttpTransportConfig>(jsonConfig, out HttpTransportConfig? httpConfig, out _))
        {
            Name = httpConfig!.Name;
            HttpEndpoint = httpConfig!.Endpoint;
            HttpHeadersText = string.Join(Environment.NewLine, httpConfig.Headers.Select(header => $"{header.Key}={header.Value}"));
            return true;
        }

        return false;
    }

    private string CreateJsonFromForm()
    {
        if (IsHttpTransport)
        {
            HttpTransportConfig httpConfig = new(Normalize(Name), Normalize(HttpEndpoint), ParseHeaders(HttpHeadersText));
            return JsonSerializer.Serialize(httpConfig, PrettyPrintOptions);
        }

        StdioConfig stdioConfig = new(Normalize(Name), Normalize(StdioCommand), ParseArguments(StdioArgumentsText));
        return JsonSerializer.Serialize(stdioConfig, PrettyPrintOptions);
    }

    private McpDefinition CreateDefinition(string name, bool isStdio, string jsonConfig)
        => new()
        {
            Id = _existingId ?? 0,
            Name = Normalize(name),
            Description = Normalize(Description),
            IsReadOnly = IsReadOnly,
            IsStdio = isStdio,
            ConfigJson = jsonConfig,
        };

    private static bool TryParseJsonConfig<T>(string json, out T? config, out string validationMessage)
    {
        try
        {
            config = JsonSerializer.Deserialize<T>(json, ReadOptions);
            if (config is null)
            {
                validationMessage = "Json config could not be parsed.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }
        catch (JsonException exception)
        {
            config = default;
            validationMessage = exception.Message;
            return false;
        }
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string[] ParseArguments(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Dictionary<string, string> ParseHeaders(string? text)
    {
        Dictionary<string, string> headers = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            return headers;
        }

        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                separatorIndex = line.IndexOf(':');
            }

            if (separatorIndex <= 0)
            {
                continue;
            }

            string key = Normalize(line[..separatorIndex]);
            string value = Normalize(line[(separatorIndex + 1)..]);
            if (key.Length > 0)
            {
                headers[key] = value;
            }
        }

        return headers;
    }
}
