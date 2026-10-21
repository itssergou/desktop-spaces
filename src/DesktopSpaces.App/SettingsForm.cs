namespace DesktopSpaces.App;

internal sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly Action _save;
    private readonly Action<Exception> _error;
    private readonly Action<Guid> _remove;
    private readonly Action<Guid,int> _move;
    private readonly Action<int> _select;
    private readonly ToolTip _tips=new() { AutoPopDelay=12000,InitialDelay=350,ReshowDelay=100 };
    private readonly ListView _list=new() { View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,Dock=DockStyle.Fill,BorderStyle=BorderStyle.FixedSingle,LabelEdit=true,AllowDrop=true };
    private readonly Button _removeButton=new() { Text="Удалить",AutoSize=true };
    private readonly Button _renameButton=new() { Text="Переименовать",AutoSize=true };
    private readonly Button _upButton=new() { Text="↑",Width=38 };
    private readonly Button _downButton=new() { Text="↓",Width=38 };
    private bool _refreshing;

    public SettingsForm(AppConfig config,bool demo,Action<string> add,Action<Guid> remove,Action<Guid,int> move,
        Action<int> select,Action save,Action<Exception> error)
    {
        _config=config;_save=save;_error=error;_remove=remove;_move=move;_select=select;
        Text="DesktopSpaces — настройки";Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        ShowInTaskbar=demo && Environment.GetCommandLineArgs().Contains("--ui-test");StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new Size(880,760);MinimumSize=new Size(860,740);
        BackColor=Color.FromArgb(244,246,249);
        var page=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(28,20,28,20),ColumnCount=1,RowCount=7 };
        foreach(var height in new[]{58,50})page.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        foreach(var height in new[]{48,160,100,40})page.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        Controls.Add(page);
        page.Controls.Add(new Label { Text="DesktopSpaces",Font=new Font("Segoe UI",24,FontStyle.Bold),AutoSize=true,ForeColor=Color.FromArgb(27,37,54) });
        page.Controls.Add(new Label { Text=demo ? "Демонстрация: используются тестовые папки. Ваш рабочий стол не меняется." : "Двойной щелчок по пустому месту рабочего стола — следующее пространство.",Dock=DockStyle.Fill,ForeColor=demo ? Color.DarkGoldenrod : Color.FromArgb(85,95,110) });
        _list.Columns.Add("Пространство · F2 — переименовать · перетащите строку для сортировки",530);
        _list.Columns.Add("Переход",225);
        _list.SelectedIndexChanged+=(_,_)=> { if(!_refreshing)UpdateButtons(); };
        _list.KeyDown+=(_,e)=> { if(e.KeyCode==Keys.F2){SelectedItem()?.BeginEdit();e.Handled=true;e.SuppressKeyPress=true;} };
        _list.AfterLabelEdit+=(_,e)=> {
            if(e.Label is null)return;
            e.CancelEdit=true;
            var space=FindSpace(_list.Items[e.Item]);
            if(space is not null)Try(()=>Rename(space,e.Label));
        };
        _list.ItemDrag+=(_,e)=> {
            if(e.Item is ListViewItem item && item.Index>0 && item.Tag is Guid id)
                _list.DoDragDrop(new DataObject("DesktopSpaces.Space",id.ToString()),DragDropEffects.Move);
        };
        _list.DragOver+=(_,e)=> {
            var point=_list.PointToClient(new Point(e.X,e.Y));var target=_list.GetItemAt(point.X,point.Y);
            e.Effect=e.Data?.GetDataPresent("DesktopSpaces.Space")==true && target?.Index!=0 ? DragDropEffects.Move : DragDropEffects.None;
            _list.InsertionMark.Index=target?.Index ?? _list.Items.Count-1;
            _list.InsertionMark.AppearsAfterItem=target is null || point.Y>target.Bounds.Top+target.Bounds.Height/2;
        };
        _list.DragLeave+=(_,_)=>_list.InsertionMark.Index=-1;
        _list.DragDrop+=(_,e)=> {
            _list.InsertionMark.Index=-1;
            if(e.Data?.GetData("DesktopSpaces.Space") is not string value || !Guid.TryParse(value,out var id))return;
            var point=_list.PointToClient(new Point(e.X,e.Y));var target=_list.GetItemAt(point.X,point.Y);
            var destination=target?.Index ?? _list.Items.Count-1;
            if(destination<=0)return;
            Try(()=>{_move(id,destination);RefreshSpaces(id);});
        };
        page.Controls.Add(_list);
        var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0),WrapContents=false };
        var addButton=new Button { Text="+ Добавить",AutoSize=true };
        addButton.Click+=(_,_)=> {
            var name=AskName("Новое пространство","Пространство "+(_config.Spaces.Count+1));
            if(name is not null)Try(()=>{add(name);RefreshSpaces(_config.Spaces[^1].Id);});
        };
        _renameButton.Click+=(_,_)=> {
            var space=SelectedSpace();if(space is null)return;
            var name=AskName("Название пространства",space.Name);
            if(name is not null)Try(()=>Rename(space,name));
        };
        _removeButton.Click+=(_,_)=> {
            var space=SelectedSpace();if(space is null)return;
            if(MessageBox.Show(this,$"Удалить пространство «{space.Name}»?\n\nФайлы и папки сохранятся здесь:\n{space.Folder}\n\nЕсли пространство открыто, приложение вернёт первый рабочий стол.","Удаление пространства",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
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
        modifier.SelectedIndexChanged+=(_,_)=>Try(()=> { var old=config.Preferences.UseControlAlt;config.Preferences.UseControlAlt=modifier.SelectedIndex==1;try{_save();}catch{config.Preferences.UseControlAlt=old;throw;}RefreshSpaces(); });
        hotkeys.Controls.Add(modifier);preferences.Controls.Add(hotkeys);
        var indicatorRow=new FlowLayoutPanel { AutoSize=true,WrapContents=false,Margin=Padding.Empty };
        indicatorRow.Controls.Add(Check("Кратко показывать название при переключении",config.Preferences.ShowIndicator,v=>config.Preferences.ShowIndicator=v));
        var help=new HelpButton { Text="?",Margin=new Padding(0,0,0,0),AccessibleName="Подсказка о названии пространства",TabStop=true };
        const string explanation="Показывает номер и имя пространства на 1,2 секунды\nв правом верхнем углу после переключения.";
        _tips.SetToolTip(help,explanation);help.AccessibleDescription=explanation;
        help.Click+=(_,_)=>_tips.Show(explanation,help,0,help.Height,8000);
        indicatorRow.Controls.Add(help);preferences.Controls.Add(indicatorRow);
        var autostart=Check("Запускать при входе в Windows",config.Preferences.AutoStart,v=>config.Preferences.AutoStart=v);autostart.Enabled=!demo;preferences.Controls.Add(autostart);
        page.Controls.Add(preferences);
        var keys=config.Preferences.UseControlAlt?"Ctrl + Alt":"Alt";
        var instruction=new Label { Dock=DockStyle.Fill,ForeColor=Color.FromArgb(85,95,110) };
        instruction.Text=$"Перенос файла: зажмите левую кнопку мыши на файле и начните его перетаскивать.\nНаведите курсор на рабочий стол, не отпуская мышь нажмите {keys} + номер пространства.\nОтпустите клавиши, затем левую кнопку мыши там, куда хотите положить файл.\nТакже можно: Ctrl + X → перейти в другое пространство → Ctrl + V. Настройки сохраняются автоматически.";
        modifier.SelectedIndexChanged+=(_,_)=>instruction.Text=instruction.Text.Replace(modifier.SelectedIndex==1?"Alt + номер":"Ctrl + Alt + номер",modifier.SelectedIndex==1?"Ctrl + Alt + номер":"Alt + номер");
        page.Controls.Add(instruction);
        var footer=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft };
        var close=new Button { Text="Готово",AutoSize=true,DialogResult=DialogResult.OK };footer.Controls.Add(close);
        var open=new Button { Text="Открыть папку",AutoSize=true };open.Click+=(_,_)=> { var space=SelectedSpace();if(space is not null)Try(()=>DesktopApplication.OpenFolder(space.Folder)); };footer.Controls.Add(open);
        var activate=new Button { Text="Перейти",AutoSize=true };activate.Click+=(_,_)=> { var space=SelectedSpace();if(space is not null)_select(_config.Spaces.IndexOf(space)); };footer.Controls.Add(activate);
        page.Controls.Add(footer);CancelButton=close;
        RefreshSpaces(config.ActiveSpaceId);
    }

    private CheckBox Check(string text,bool value,Action<bool> assign)
    {
        var box=new CheckBox { Text=text,Checked=value,AutoSize=true,Margin=new Padding(0,2,8,3) };var updating=false;
        box.CheckedChanged+=(_,_)=> {
            if(updating)return;
            var previous=!box.Checked;
            try { assign(box.Checked);_save();RefreshSpaces(); }
            catch(Exception ex) { assign(previous);updating=true;box.Checked=previous;updating=false;ShowError(ex); }
        };return box;
    }
    private ListViewItem? SelectedItem()=>_list.SelectedItems.Count==0?null:_list.SelectedItems[0];
    private Space? FindSpace(ListViewItem? item)=>item?.Tag is Guid id ? _config.Spaces.FirstOrDefault(s=>s.Id==id) : null;
    private Space? SelectedSpace()=>FindSpace(SelectedItem());
    private void Rename(Space space,string name)
    {
        name=name.Trim();if(name.Length is <1 or >80)throw new ArgumentException("Название должно содержать от 1 до 80 символов.");
        var old=space.Name;space.Name=name;try{_save();}catch{space.Name=old;throw;}
        // Rebuilding the list inside LVN_ENDLABELEDIT can destroy the edit control.
        BeginInvoke((Action)(()=>{if(!IsDisposed)RefreshSpaces(space.Id);}));
    }
    private void ChangeOrder(int delta) { var space=SelectedSpace();if(space is not null)Try(()=>{_move(space.Id,_config.Spaces.IndexOf(space)+delta);RefreshSpaces(space.Id);}); }
    internal void RefreshSpaces(Guid? selection=null)
    {
        var id=selection ?? (SelectedItem()?.Tag as Guid?);
        _refreshing=true;_list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            for(var i=0;i<_config.Spaces.Count;i++)
            {
                var space=_config.Spaces[i];var item=new ListViewItem(space.Name) { Tag=space.Id };
                item.SubItems.Add(i<9 && _config.Preferences.HotkeysEnabled ? (_config.Preferences.UseControlAlt?"Ctrl + Alt + ":"Alt + ")+(i+1) : "Через меню трея");
                _list.Items.Add(item);if(space.Id==id){item.Selected=true;item.Focused=true;}
            }
            if(_list.SelectedItems.Count==0 && _list.Items.Count>0)_list.Items[0].Selected=true;
        }
        finally { _list.EndUpdate();_refreshing=false; }
        UpdateButtons();
    }
    private void UpdateButtons()
    {
        var space=SelectedSpace();var index=space is null?-1:_config.Spaces.IndexOf(space);
        _renameButton.Enabled=index>=0;_removeButton.Enabled=index>0;
        _upButton.Enabled=index>1;_downButton.Enabled=index>0 && index<_config.Spaces.Count-1;
    }
    private void Try(Action action) { try{action();}catch(Exception ex){ShowError(ex);} }
    private void ShowError(Exception ex) { _error(ex);MessageBox.Show(this,ex.Message,"DesktopSpaces",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    private string? AskName(string title,string initial)
    {
        using var dialog=new Form { Text=title,ShowInTaskbar=ShowInTaskbar,FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(440,154),MinimizeBox=false,MaximizeBox=false,Font=Font,AutoScaleMode=AutoScaleMode.Dpi };
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=3,RowCount=3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var field=new TextBox { Text=initial,MaxLength=80,Dock=DockStyle.Top,Margin=Padding.Empty };
        layout.Controls.Add(field,0,0);layout.SetColumnSpan(field,3);
        var actions=new FlowLayoutPanel { AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Margin=Padding.Empty,Anchor=AnchorStyles.None };
        var ok=new Button { Text="Сохранить",DialogResult=DialogResult.OK,Size=new Size(116,34),Margin=new Padding(0,0,6,0) };
        var cancel=new Button { Text="Отмена",DialogResult=DialogResult.Cancel,Size=new Size(116,34),Margin=new Padding(6,0,0,0) };
        actions.Controls.AddRange([ok,cancel]);layout.Controls.Add(actions,1,2);
        dialog.Controls.Add(layout);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
        field.TextChanged+=(_,_)=>ok.Enabled=!string.IsNullOrWhiteSpace(field.Text);
        dialog.Shown+=(_,_)=>{field.Focus();field.SelectAll();};
        return dialog.ShowDialog(this)==DialogResult.OK ? field.Text.Trim() : null;
    }
    protected override void Dispose(bool disposing) { if(disposing)_tips.Dispose();base.Dispose(disposing); }
}
