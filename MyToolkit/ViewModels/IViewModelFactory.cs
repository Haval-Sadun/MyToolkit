using Microsoft.Extensions.DependencyInjection;

namespace MyToolkit.ViewModels;

public interface IViewModelFactory
{
    T Create<T>() where T : ToolkitViewModel;
}

public class ViewModelFactory(IServiceProvider serviceProvider) : IViewModelFactory
{
    public T Create<T>() where T : ToolkitViewModel =>
        serviceProvider.GetRequiredService<T>();
}
