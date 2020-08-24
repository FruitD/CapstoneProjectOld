/*
 * Created by Levi Delezene 
 */

using CapstoneProject.DAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//added namespace-alankar
namespace CapstoneProject.Models
{
    public class User
    {
        public User(int id, string FirstName, String LastName)
        {
            this.Id = id;
            this.FirstName = FirstName;
            this.LastName = LastName;
        }

        public User()
        {

        }

        public int Id { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }

        public string FullNameMiddleInitial
        {
            //This is called string interpolation.
            //It's much cleaner than doing FirstName + " " + MiddleName[0] + ". " + LastName
            get { return $"{FirstName} {MiddleName[0]}. {LastName}"; }
        }

        public string FullNameNoMiddle
        {
            get { return $"{FirstName} {LastName}"; }
        }

        public string FullName
        {
            get { return $"{FirstName} {MiddleName} {LastName}"; }
        }

        public override string ToString()
        {
            return FullNameNoMiddle;
        }

        public override bool Equals(object obj)
        {
            User user = (User)obj;
            if (this.FirstName == user.FirstName && this.MiddleName == user.MiddleName && this.LastName == user.LastName)
                return true;
            else
                return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public User save() {
            OUser oUser = new OUser();
            if (oUser.Get(Id) == null) {
                oUser.Insert(this);
            } else {
                oUser.Update(this);
            }
            return this;
        }
    }
}