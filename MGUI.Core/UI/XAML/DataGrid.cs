namespace MGUI.Core.UI.XAML;

public class DataGrid : ListView
{
    public override MGElementType ElementType => MGElementType.ListView;

    protected override MGElement CreateElementInstance(MGWindow Window, MGElement Parent)
    {
        var genericType = typeof(MGDataGrid<>).MakeGenericType(new Type[] { ItemType });
        var element = Activator.CreateInstance(genericType, new object[] { Window });
        return element as MGElement;
    }
}