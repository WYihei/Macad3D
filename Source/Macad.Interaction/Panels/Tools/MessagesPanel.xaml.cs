using System;
using System.Collections.Specialized;
using System.Text;
using System.Windows.Controls;
using Macad.Core;
using Macad.Core.Shapes;
using Macad.Core.Topology;

namespace Macad.Interaction.Panels;

public partial class MessagesPanel : UserControl
{
    public MessagesPanel()
    {
        InitializeComponent();

        var handler = CoreContext.Current.MessageHandler;
        handler.MessageItems.CollectionChanged += MessageItems_CollectionChanged;
    }

    //--------------------------------------------------------------------------------------------------

    void MessageItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            var sb = new StringBuilder();
            foreach (MessageItem item in e.NewItems)
            {
                FormatMessage(sb, item);
            }
            textBox.AppendText(sb.ToString());
        }
        else
        {
            // Reset or other action: rebuild entire text
            var sb = new StringBuilder();
            foreach (var item in CoreContext.Current.MessageHandler.MessageItems)
            {
                FormatMessage(sb, item);
            }
            textBox.Text = sb.ToString();
        }

        textBox.ScrollToEnd();
    }

    //--------------------------------------------------------------------------------------------------

    static void FormatMessage(StringBuilder sb, MessageItem message)
    {
        string senderName = "";
        if (message.Sender != null && message.Sender.TryGetTarget(out Entity entity))
        {
            if (entity is Shape shape && shape.Body != null)
            {
                senderName = $"[{shape.Body.Name}] ";
            }
        }

        sb.Append($"[{message.TimeStamp:HH:mm:ss}] [{message.Severity}] {senderName}{message.Text}\n");

        if (message.Explanation is { Length: > 0 })
        {
            foreach (var line in message.Explanation)
            {
                sb.Append($"  {line}\n");
            }
        }
    }
}
