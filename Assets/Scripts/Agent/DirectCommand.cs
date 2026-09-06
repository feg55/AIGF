using System.Text.RegularExpressions;
using Aigf.Companion.AI;
using Aigf.Companion.Room;

namespace Aigf.Companion.Agent
{
    public enum CompanionCommand { ComeHere, Sit, Stand, Follow, Stop, Wave, LookAtUser }

    public static class DirectCommand
    {
        public static AgentReply Create(CompanionCommand command, RoomGraph room, bool chair = false)
        {
            switch (command)
            {
                case CompanionCommand.ComeHere: return Action(AgentActionTypes.WalkToUser);
                case CompanionCommand.Stand: return Action(AgentActionTypes.Stand);
                case CompanionCommand.Follow: return Action(AgentActionTypes.FollowUser);
                case CompanionCommand.Stop: return Action(AgentActionTypes.Stop);
                case CompanionCommand.Wave: return Action(AgentActionTypes.Wave);
                case CompanionCommand.LookAtUser: return Action(AgentActionTypes.LookAtUser);
                case CompanionCommand.Sit:
                    var seat = chair ? room?.FindFirst(RoomNodeType.Chair) :
                        room?.FindFirst(RoomNodeType.Sofa) ?? room?.FindFirst(RoomNodeType.Chair);
                    return seat != null
                        ? new AgentReply(string.Empty, "neutral", new AgentAction(AgentActionTypes.Sit, seat.Id))
                        : new AgentReply("Не вижу подходящего места для сидения.", "neutral");
                default: return new AgentReply();
            }
        }

        public static bool TryParse(string message, RoomGraph room, out AgentReply reply)
        {
            reply = null;
            var text = Regex.Replace((message ?? string.Empty).ToLowerInvariant().Replace('ё', 'е'), @"[^\p{L}\p{Nd}\s]", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            text = Regex.Replace(text, @"^(?:(?:минт|mint|пожалуйста|please) )+| (?:пожалуйста|please)$", "").Trim();
            CompanionCommand command;
            // Match the entire imperative. Mentions/questions/negations remain
            // conversation, e.g. "не садись" or "почему ты сидишь на диване?".
            if (Match(text, @"стоп|остановись|стой|stop|halt")) command = CompanionCommand.Stop;
            else if (Match(text, @"встань|вставай|stand(?: up)?|get up")) command = CompanionCommand.Stand;
            else if (Match(text, @"(?:иди|подойди) ко мне|подойди|come here|come to me")) command = CompanionCommand.ComeHere;
            else if (Match(text, @"(?:иди|следуй) за мной|follow(?: me)?")) command = CompanionCommand.Follow;
            else if (Match(text, @"помаши(?: мне| рукой)?|wave(?: at me)?")) command = CompanionCommand.Wave;
            else if (Match(text, @"(?:посмотри|смотри) на меня|look at me")) command = CompanionCommand.LookAtUser;
            else if (Match(text, @"(?:сядь|садись|присядь)(?: на (?:диван|диване|софу|стул|стуле|кресло|кресле))?|sit(?: down)?(?: on (?:the )?(?:sofa|couch|chair))?")) command = CompanionCommand.Sit;
            else return false;
            reply = Create(command, room, Match(text, @".*(?:стул|кресл|chair).*"));
            return true;
        }

        private static bool Match(string value, string pattern) => Regex.IsMatch(value, @"\A(?:" + pattern + @")\z", RegexOptions.CultureInvariant);
        private static AgentReply Action(string type) => new AgentReply(string.Empty, "neutral", new AgentAction(type));
    }
}
