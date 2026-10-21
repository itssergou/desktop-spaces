namespace DesktopSpaces.App;

internal sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly Action _save;
    private readonly Action<Exception> _error;
    private readonly Action<Guid> _remove;
    private readonly Action<Guid,int> _move;
    private readonly Action<int> _select;
    private readonly ListView _list=new() { View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None };
    private readonly Button _removeButton=new() { Text="Убрать из списка",AutoSize=true };
    private readonly Button _renameButton=new() { Text="Переименовать",AutoSize=true };
    private readonly Button _upButton=new() { Text="↑",Width=38 };
    private readonly Button _downButton=new() { Text="↓",Width=38 };

    public SettingsForm(AppConfig config,bool demo,Action<string> add,Action<Guid> remove,Action<Guid,int> move,
        Action<int> select,Action save,Action<Exception> error)
    {
        _config=config;_save=save;_error=error;_remove=remove;_move=move;_select=select;
        Text="DesktopSpaces — настройки";Font=new Font("Segoe UI",10);
        ShowInTaskbar=demo && Environment.GetCommandLineArgs().Contains("--ui-test");StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new Size(850,700);MinimumSize=new Size(820,690);
        BackColor=Color.FromArgb(244,246,249);
        var page=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(28,20,28,20),ColumnCount=1,RowCount=7 };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
        page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,150));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        Controls.Add(page);
        page.Controls.Add(new Label { Text="DesktopSpaces",Font=new Font("Segoe UI",24,FontStyle.Bold),AutoSize=true,ForeColor=Color.FromArgb(27,37,54) });
        page.Controls.Add(new Label { Text=demo ? "Демонстрация: используются тестовые папки. Ваш рабочий стол не меняется." : "Ваши пространства · двойной щелчок по пустому месту — следующее",Dock=DockStyle.Fill,ForeColor=demo ? Color.DarkGoldenrod : Color.FromArgb(85,95,110) });
        _list.Columns.Add("Пространство",470);_list.Columns.Add("Переход",210);
        _list.SelectedIndexChanged+=(_,_)=>UpdateButtons();
        _list.DoubleClick+=(_,_)=> { if(_list.SelectedIndices.Count>0)_select(_list.SelectedIndices[0]); };
        page.Controls.Add(_list);
        var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0),WrapContents=false };
        var addButton=new Button { Text="+ Добавить",AutoSize=true };
        addButton.Click+=(_,_)=> {
            var name=AskName("Новое пространство","Пространство "+(_config.Spaces.Count+1));
            if(name is null)return;
            Try(()=>{add(name);RefreshSpaces(_config.Spaces[^1].Id);});
        };
        _renameButton.Click+=(_,_)=> {
            var space=SelectedSpace();if(space is null)return;
            var name=AskName("Название пространства",space.Name);if(name is null)return;
            Try(()=>{var old=space.Name;space.Name=name;try{_save();}catch{space.Name=old;throw;}RefreshSpaces(space.Id);});
        };
        _removeButton.Click+=(_,_)=> {
            var space=SelectedSpace();if(space is null)return;
            if(MessageBox.Show(this,$"Убрать «{space.Name}» из списка?\nФайлы останутся в папке. Она откроется после исключения.","Сохранить файлы и убрать пространство",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
            Try(()=>{_remove(space.Id);RefreshSpaces();});
        };
        _upButton.Click+=(_,_)=>ChangeOrder(-1);_downButton.Click+=(_,_)=>ChangeOrder(1);
        buttons.Controls.AddRange([addButton,_renameButton,_upButton,_downButton,_removeButton]);page.Controls.Add(buttons);
        var preferences=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,8,0,0) };
        preferences.Controls.Add(Check("Переключать двойным щелчком по пустому месту",config.Preferences.DoubleClickSwitch,v=>config.Preferences.DoubleClickSwitch=v));
        var hotkeys=new FlowLayoutPanel { AutoSize=true,WrapContents=false,Margin=Padding.Empty };
        hotkeys.Controls.Add(Check("Переключать с клавиатуры",config.Preferences.HotkeysEnabled,v=>config.Preferences.HotkeysEnabled=v));
        var modifier=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=190 };
        modifier.Items.AddRange(["Alt + 1…9","Ctrl + Alt + 1…9"]);modifier.SelectedIndex=config.Preferences.UseControlAlt?1:0;
        modifier.SelectedIndexChanged+=(_,_)=>Try(()=> { config.Preferences.UseControlAlt=modifier.SelectedIndex==1;_save();RefreshSpaces(); });
        hotkeys.Controls.Add(modifier);preferences.Controls.Add(hotkeys);
        preferences.Controls.Add(Check("Кратко показывать название при переключении",config.Preferences.ShowIndicator,v=>config.Preferences.ShowIndicator=v));
        var autostart=Check("Запускать при входе в Windows",config.Preferences.AutoStart,v=>config.Preferences.AutoStart=v);autostart.Enabled=!demo;preferences.Controls.Add(autostart);
        page.Controls.Add(preferences);
        page.Controls.Add(new Label { Dock=DockStyle.Fill,ForeColor=Color.FromArgb(85,95,110),Text="Для переноса: удерживайте элемент мышью, нажмите Alt + номер, отпустите Alt и положите элемент.\nНастройки сохраняются автоматически. При выходе возвращается первый рабочий стол." });
        var footer=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft };
        var close=new Button { Text="Готово",AutoSize=true,DialogResult=DialogResult.OK };footer.Controls.Add(close);
        var open=new Button { Text="Открыть папку",AutoSize=true };open.Click+=(_,_)=> { var space=SelectedSpace();if(space is not null)Try(()=>DesktopApplication.OpenFolder(space.Folder)); };footer.Controls.Add(open);
        var activate=new Button { Text="Перейти",AutoSize=true };activate.Click+=(_,_)=> { if(_list.SelectedIndices.Count>0)_select(_list.SelectedIndices[0]); };footer.Controls.Add(activate);
        page.Controls.Add(footer);AcceptButton=close;CancelButton=close;
        RefreshSpaces(config.ActiveSpaceId);
    }

    private CheckBox Check(string text,bool value,Action<bool> assign)
    {
        var box=new CheckBox { Text=text,Checked=value,AutoSize=true,Margin=new Padding(0,2,8,3) };
        box.CheckedChanged+=(_,_)=>Try(()=>{assign(box.Checked);_save();RefreshSpaces();});return box;
    }
    private Space? SelectedSpace() => _list.SelectedIndices.Count==0 ? null : _config.Spaces[_list.SelectedIndices[0]];
    private void ChangeOrder(int delta) { var space=SelectedSpace();if(space is not null)Try(()=>{_move(space.Id,delta);RefreshSpaces(space.Id);}); }
    private void RefreshSpaces(Guid? selection=null)
    {
        var id=selection ?? SelectedSpace()?.Id;
        _list.BeginUpdate();_list.Items.Clear();
        for(var i=0;i<_config.Spaces.Count;i++)
        {
            var space=_config.Spaces[i];
            var item=new ListViewItem($"{i+1}.  {space.Name}");
            item.SubItems.Add(i<9 && _config.Preferences.HotkeysEnabled ? (_config.Preferences.UseControlAlt?"Ctrl + Alt + ":"Alt + ")+(i+1) : "Через меню трея");
            _list.Items.Add(item);if(space.Id==id)item.Selected=true;
        }
        if(_list.SelectedIndices.Count==0 && _list.Items.Count>0)_list.Items[0].Selected=true;
        _list.EndUpdate();UpdateButtons();
    }
    private void UpdateButtons()
    {
        var index=_list.SelectedIndices.Count==0 ? -1 : _list.SelectedIndices[0];
        _renameButton.Enabled=index>=0;_removeButton.Enabled=index>0 && _config.Spaces[index].Id!=_config.ActiveSpaceId;
        _upButton.Enabled=index>1;_downButton.Enabled=index>0 && index<_config.Spaces.Count-1;
    }
    private void Try(Action action)
    {
        try{action();}catch(Exception ex){_error(ex);MessageBox.Show(this,ex.Message,"DesktopSpaces",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private string? AskName(string title,string initial)
    {
        using var dialog=new Form { Text=title,ShowInTaskbar=ShowInTaskbar,FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(420,130),MinimizeBox=false,MaximizeBox=false,Font=Font };
        var field=new TextBox { Text=initial,MaxLength=80,Location=new Point(20,20),Width=380 };
        var ok=new Button { Text="Сохранить",DialogResult=DialogResult.OK,Location=new Point(200,75),Width=100 };
        var cancel=new Button { Text="Отмена",DialogResult=DialogResult.Cancel,Location=new Point(310,75),Width=90 };
        dialog.Controls.AddRange([field,ok,cancel]);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
        dialog.Shown+=(_,_)=>{field.Focus();field.SelectAll();};
        return dialog.ShowDialog(this)==DialogResult.OK && !string.IsNullOrWhiteSpace(field.Text) ? field.Text.Trim() : null;
    }
}
