﻿using System;
using System.Windows.Data;

namespace ARK_Server_Manager.Lib.ViewModel
{

    public class MinutesToTimeValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // Value is seconds since midnight.
            var totalMinutes = (int)value;
            var hours = Math.Min(Math.Max(totalMinutes / 60, 0), 23);
            var minutes = Math.Min(Math.Max(totalMinutes % 60, 0), 59);
            return String.Format("{0:00}:{1:00}", hours, minutes);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var strTime = (string)value;
            var split = strTime.Split(':');
            if(split.Length != 2)
            {
                return 0;
            }

            int hours;
            Int32.TryParse(split[0], out hours);

            int minutes;
            Int32.TryParse(split[1], out minutes);

            return hours * 60 + minutes;
        }
    }
}
