namespace TTBot.Constants
{
    public static class BotConstants
    {
        public const int _insurancePrice = 100;

        public const string _startMessage = """
                <b><u>Hello. I'm a car insurance bot</u></b>
                My purpose is to assist you with
                buying an insurance for your car
                Plese type /menu to get futher instructions
            """;

        public const string _usageMessage = """
                <b><u>Bot menu</u></b>:
                /start   - start conversation
                /send    - get information about required documents
                /restart - remove send data and fill it one more time
            """;

        public const string _sendPhotoMessage = """
                Currently you have to upload 2 photos:
                1) Your passport data
                2) Your vehicle identification document
            """;

        public const string _removeDataMessage = """
                Your data has been cleared.
                Now you can send it one more time
            """;

        public const string _photoesAllreadyMade = """
                Sorry, but you already field and confirmed your data
                if something is wrong and you want to send
                passport and vehickle data one more time
                please type "/restart"
            """;
    }
}
