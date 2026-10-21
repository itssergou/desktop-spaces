namespace DesktopSpaces.App;

// The colour-keyed icon surface leaves live wallpaper visible. This almost fully
// transparent sibling receives background clicks and OLE drops in those holes.
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class DesktopInputSurface : Form,IDropTargetNative
{
    private readonly Func<ShellFolderView?> _active;
    private IDropTargetNative? _target;
    private System.Runtime.InteropServices.ComTypes.IDataObject? _data;
    private Guid? _targetId;
    private string[] _files=[];
    public Func<System.Runtime.InteropServices.ComTypes.IDataObject,System.Runtime.InteropServices.ComTypes.IDataObject>? PrepareData { get; set; }
    public DesktopInputSurface(Func<ShellFolderView?> active)
    {
        _active=active;TopLevel=false;ShowInTaskbar=false;FormBorderStyle=FormBorderStyle.None;BackColor=Color.Black;
    }
    public void EnableTransparency()
    {
        Native.SetWindowLongPtr(Handle,-20,new IntPtr(Native.GetWindowLongPtr(Handle,-20).ToInt64()|0x80000));
        if(!Native.SetLayeredWindowAttributes(Handle,0,1,2))throw new System.ComponentModel.Win32Exception();
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.Check(Native.RegisterDragDrop(Handle,this));
    }
    protected override void WndProc(ref Message m)
    {
        var view=_active();
        if(view is not null && m.Msg is >=0x200 and <=0x20E)
        {
            var list=view.ListHandle;
            if(list!=IntPtr.Zero)
            {
                var point=new Native.Point((short)(m.LParam.ToInt64()&0xFFFF),(short)((m.LParam.ToInt64()>>16)&0xFFFF));
                if(m.Msg is not (0x20A or 0x20E))Native.MapWindowPoints(Handle,list,ref point,1);
                var position=new IntPtr((point.X&0xFFFF)|((point.Y&0xFFFF)<<16));
                m.Result=Native.SendMessage(list,(uint)m.Msg,m.WParam,position);return;
            }
        }
        if(view is not null && m.Msg==0x7B)
        { m.Result=Native.SendMessage(view.ListHandle,(uint)m.Msg,view.ListHandle,m.LParam);return; }
        base.WndProc(ref m);
    }
    private void ReleaseTarget(bool notify=true)
    {
        if(_target is not null)
        {
            if(notify)_target.DragLeave();
            if(Marshal.IsComObject(_target))Marshal.ReleaseComObject(_target);
        }
        _target=null;_targetId=null;
    }
    private bool EnsureTarget(uint keys,Native.Point point,ref uint effect)
    {
        var view=_active();
        if(view is null){effect=0;return false;}
        if((keys&8)==0 && view.ContainsFiles(_files) && !view.IsFolderTarget(point)){ReleaseTarget();effect=2;return false;}
        if(_targetId!=view.Space.Id)
        {
            ReleaseTarget();_target=view.GetDropTarget();_targetId=view.Space.Id;
            if(_target is not null && _data is not null)Native.Check(_target.DragEnter(_data,keys,point,ref effect));
        }
        if(_target is null)effect=0;
        return _target is not null;
    }
    int IDropTargetNative.DragEnter(System.Runtime.InteropServices.ComTypes.IDataObject data,uint keys,Native.Point point,ref uint effect)
    { try { _data=PrepareData?.Invoke(data) ?? data;_files=OleFiles.Read(_data);TransferTrace.Write("drag-enter",new {Target=_active()?.Space.Folder,Files=TransferTrace.Files(_files),keys,effect});EnsureTarget(keys,point,ref effect);return 0; }catch(Exception ex) { TransferTrace.Write("drag-enter-error",ex.ToString());effect=0;return 0; } }
    int IDropTargetNative.DragOver(uint keys,Native.Point point,ref uint effect)
    { try { return EnsureTarget(keys,point,ref effect) ? _target!.DragOver(keys,point,ref effect) : 0; }catch { effect=0;return 0; } }
    int IDropTargetNative.DragLeave() { ReleaseTarget();_data=null;_files=[];return 0; }
    public int Drop(System.Runtime.InteropServices.ComTypes.IDataObject data,uint keys,Native.Point point,ref uint effect)
    {
        try {
            TransferTrace.Write("drop",new {Target=_active()?.Space.Folder,Files=TransferTrace.Files(_files),CurrentFiles=TransferTrace.Files(OleFiles.Read(data)),keys,effect});
            if((keys&8)==0 && _active() is { } current && !current.IsFolderTarget(point) && current.MoveIcons(_files,point)){effect=2;return 0;}
            if(!EnsureTarget(keys,point,ref effect))return 0;
            var destination=_active();
            var dropPoint=destination?.FreeDropPoint(_files,point) ?? point;
            var place=destination?.PrepareIncomingPlacement(_files,dropPoint);
            Native.Check(_target!.DragOver(keys,dropPoint,ref effect));
            var result=_target.Drop(_data ?? data,keys,dropPoint,ref effect);
            if(result>=0)place?.Invoke();
            return result;
        }
        catch(Exception ex) { TransferTrace.Write("drop-error",ex.ToString());effect=0;return 0; }
        finally { ReleaseTarget(false);_data=null;_files=[]; }
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) { ReleaseTarget();if(IsHandleCreated)Native.RevokeDragDrop(Handle); }
        base.Dispose(disposing);
    }
}

