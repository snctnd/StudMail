﻿using MailClient.DB;
using MailClient.Helpers;
using MailKit.Net.Smtp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MailClient
{
    public partial class LoginForm : Form
    {
        public DBConnection DB;

        public LoginForm()
        {
            InitializeComponent();
            DB = new DBConnection();
        }

        // TODO:
        private void signInBtn_Click(object sender, EventArgs e)
        {
            var email = emailTxt.Text.Trim(' ');
            var password = passwordTxt.Text;
            ServerInfo mailServer;
            UserInfo user = new UserInfo();
            int serverId;

            if (string.IsNullOrEmpty(email))
            {
                checkInputPrv.SetError(emailTxt, "Заполните данное поле");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                checkInputPrv.SetError(passwordTxt, "Заполните данное поле");
                return;
            }

            if (!AuthHelper.IsValidEmail(email))
            {
                checkInputPrv.SetError(emailTxt, "Неверный адрес");
                return;
            }

            var domain = AuthHelper.GetDomain(emailTxt.Text);
            mailServer = DBServers.GetServerInfo(DBConnection.Connection, domain);

            // Проверка поддерживаемых серверов

            if (mailServer == null) // Если не нашли соответствующий сервер
            {
                MessageBox.Show("Данный сервер не поддерживается на данный момент");
                return;
            }

            serverId = (int) mailServer.Id;

            // Проверка соединения если нашли поддерживаемый сервер

            if (isNetwork())    // Если есть соединение
            {
                try
                {
                    using (var client = new SmtpClient())
                    {
                        client.ServerCertificateValidationCallback = (s, c, h, ex) => true;
                        client.Connect(mailServer.SmtpHost, (int) mailServer.SmtpPort, true);
                        client.Authenticate(emailTxt.Text, passwordTxt.Text);
                        client.Disconnect(true);

                        // Регистрируем или перезаписываем пользователя в базе данных

                        user = DBUsers.AuthUser(DBConnection.Connection, email, password, serverId);
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Ошибка соединения!");
                    checkInputPrv.SetError(passwordTxt, "Неверный email или пароль");
                    return;
                }
            }
            else    // Если всё-таки нет соединения
            {
                // Проверяем наличие данного пользователя в БД
                // Если есть, то авторизуемся локально и загружаем письма
                // Если нет, то нужно вывести ошибку

                if (DBUsers.GetLoginAccount(DBConnection.Connection, email, password, serverId) != null)
                {
                    user = DBUsers.AuthUser(DBConnection.Connection, email, password, serverId);
                }
                else
                {
                    checkInputPrv.SetError(emailTxt, "Локальная учётная запись с такими данными не существует");
                    return;
                }
            }

            passwordTxt.Text = "";
            Hide();
            new ClientForm(user).ShowDialog(this);
            Show();
        }

        private void emailTxt_Enter(object sender, EventArgs e)
        {
            checkInputPrv.Clear();
        }

        private void passwordTxt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) signInBtn_Click(null, null);
        }

        private void exitBtn_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private bool isNetwork()
        {
            var ping = new Ping();
            var pingReply = ping.Send("google.com");

            return pingReply.Status.ToString().Equals("Success");
        }

        private void LoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            DBConnection.Close();
        }
    }
}
