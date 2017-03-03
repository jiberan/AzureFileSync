using GoMonkeys.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GoMonkeys.Models;
using Xamarin.Forms;

namespace GoMonkeys.Views
{
    public partial class MonkeysPage : ContentPage
    {
        MonkeysPageViewModel vm;
        public MonkeysPage()
        {
            InitializeComponent();
            BindingContext = vm = new MonkeysPageViewModel();
        }

        protected async override void OnAppearing()
        {
  


            await App.MonkeyDataManager.SyncAsync();

            await vm.LoadMonkeys();

            Monkey monkey = new Monkey();
            monkey.Status = "new";
            monkey.UserName = "john";

            TodoItem todoItem1 = new TodoItem();
            todoItem1.Id = Guid.NewGuid().ToString();
            todoItem1.Name = "todoItem1";
            todoItem1.Done = false;

            TodoItem todoItem2 = new TodoItem();
            todoItem2.Id = Guid.NewGuid().ToString();
            todoItem2.Name = "todoItem2";
            todoItem2.Done = false;

            monkey.TodoItems = new List<TodoItem>() {todoItem1, todoItem2};

            /*await App.MonkeyDataManager.SaveTodoItemAsync(todoItem1);
            await App.MonkeyDataManager.SaveTodoItemAsync(todoItem2);*/
            await App.MonkeyDataManager.SaveMonkeyAsync(monkey);




            await App.MonkeyDataManager.SyncAsync();
            base.OnAppearing();
        }
        private async void MenuItem_OnClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AddMonkeyPage());
        }
    }
}
