// ??????????????????????????????????????????????????????????????????
//  √÷› «·ﬂÊœ œÂ ›Ì ¬Œ— TransactionsForm.cs
//  ﬁ»· ¬Œ— } («··Ì » €·ﬁ «·‹ namespace)
// ??????????????????????????????????????????????????????????????????

public class DatePickerForm : Form
{
    public DateTime SelectedDate { get; private set; } = DateTime.Today;
    private DateTimePicker dtPicker;

    public DatePickerForm()
    {
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Text = "ÿ»«⁄…  ﬁ—Ì— «·”Ã·";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(360, 180);
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ColorTranslator.FromHtml("#F8FAFC");

        var lblDate = new Label
        {
            Text = "«Œ —  «—ÌŒ «· ﬁ—Ì—:",
            Font = new Font("Cairo", 10F, FontStyle.Bold),
            ForeColor = ColorTranslator.FromHtml("#0F172A"),
            Location = new Point(12, 16),
            AutoSize = true
        };

        dtPicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today,
            Font = new Font("Cairo", 11F),
            Location = new Point(12, 42),
            Width = 320
        };

        var btnOk = new Button
        {
            Text = "ÿ»«⁄… / Õ›Ÿ PDF",
            Font = new Font("Cairo", 10F, FontStyle.Bold),
            Size = new Size(150, 36),
            Location = new Point(182, 90),
            BackColor = ColorTranslator.FromHtml("#1a2f5e"),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (s, e) =>
        {
            SelectedDate = dtPicker.Value.Date;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCancel = new Button
        {
            Text = "≈·€«¡",
            Font = new Font("Cairo", 10F),
            Size = new Size(80, 36),
            Location = new Point(94, 90),
            BackColor = ColorTranslator.FromHtml("#E2E8F0"),
            ForeColor = ColorTranslator.FromHtml("#374151"),
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(lblDate);
        Controls.Add(dtPicker);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
    }
}

// ??????????????????????????????????????????????????????????????????
//  »⁄œ «·ﬂÊœ œÂ √€·ﬁ «·‹ namespace »‹ }
// ??????????????????????????????????????????????????????????????????