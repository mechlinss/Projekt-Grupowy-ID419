using System.ComponentModel.Composition;
namespace Contracts;

[InheritedExport(typeof(IWidget))]
public interface IWidget
{
    string Name { get; }
    object View { get; }
}
