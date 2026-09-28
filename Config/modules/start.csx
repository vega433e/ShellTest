void OnInit()
{
    Shell.Log("Start button module ready");
}

void OnClick()
{
    Shell.Log("Opening Windows Start menu");
    Shell.SendWinKey();
}