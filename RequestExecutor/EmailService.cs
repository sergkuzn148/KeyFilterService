using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MimeKit;
using MailKit.Net.Smtp;

namespace RequestExecutor
{
    public class EmailService
    {
        public void SendEmail (string email,string file)
        {

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("no_reply@kosmosnimki.ru"));
            emailMessage.To.Add(new MailboxAddress(email));
            emailMessage.Subject = "Запрос";
            emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Plain)
            {
                Text = $@"Данные из таблицы сохранены в файл. Вы можете скачать его из хранилища. Для этого вам необходимо перейти по ссылке: https://kosmosnimki.ru/downloads/ais/{file}" 
            };

            using(var client = new SmtpClient()){

                client.Connect("192.168.5.167", 25, false);
                client.Send(emailMessage);
                client.Disconnect(true);
            }
        }
    }
}