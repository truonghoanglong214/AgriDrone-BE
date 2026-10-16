using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AgriDrone.Api.ModelBinding;

public sealed class StringEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var enumType = Nullable.GetUnderlyingType(context.Metadata.ModelType)
            ?? context.Metadata.ModelType;
        return enumType.IsEnum ? new StringEnumModelBinder(enumType) : null;
    }

    private sealed class StringEnumModelBinder(Type enumType) : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueResult = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if (valueResult == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(
                bindingContext.ModelName,
                valueResult);

            var text = valueResult.FirstValue;
            if (string.IsNullOrWhiteSpace(text))
            {
                if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
                    bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            if (!long.TryParse(text, out _) &&
                !ulong.TryParse(text, out _) &&
                TryParseEnumName(text, out var parsed))
            {
                bindingContext.Result = ModelBindingResult.Success(parsed);
                return Task.CompletedTask;
            }

            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"'{text}' is not a valid {enumType.Name} name.");
            return Task.CompletedTask;
        }

        private bool TryParseEnumName(string text, out object? parsed)
        {
            foreach (var name in Enum.GetNames(enumType))
            {
                var apiName = JsonNamingPolicy.SnakeCaseUpper.ConvertName(name);
                if (!string.Equals(text, apiName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(text, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                parsed = Enum.Parse(enumType, name);
                return true;
            }

            parsed = null;
            return false;
        }
    }
}
