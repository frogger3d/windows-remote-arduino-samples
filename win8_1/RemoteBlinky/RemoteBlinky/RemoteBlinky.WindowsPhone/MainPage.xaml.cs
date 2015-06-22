using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RemoteBlinky
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //Usb is not supported on Win8.1. To see the USB connection steps, refer to the win10 solution instead.
        BluetoothSerial bluetooth;
        RemoteDevice arduino;

        public MainPage()
        {
            this.InitializeComponent();

                /*
                 * I've written my bluetooth device name as a parameter to the BluetoothSerial constructor. You should change this to your previously-paired
                 * device name if using Bluetooth. You can also use the BluetoothSerial.listAvailableDevicesAsync() function to list
                 * available devices, but that is not covered in this sample.
                 */
                bluetooth = new BluetoothSerial( "RNBT-7BBE" );

                arduino = new RemoteDevice( bluetooth );
                bluetooth.ConnectionEstablished += OnConnectionEstablished;

                //these parameters don't matter for bluetooth
                bluetooth.begin( 115200, SerialConfig.SERIAL_8N1 );
        }

        private void OnConnectionEstablished()
        {
            //enable the buttons on the UI thread!
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                var buttons = grid.Children.OfType<Control>();
                foreach(var b in buttons)
                {
                    b.IsEnabled = true;
                }

                arduino.pinMode(dir_latch, PinMode.OUTPUT);
                arduino.pinMode(dir_clk, PinMode.OUTPUT);
                arduino.pinMode(dir_enable, PinMode.OUTPUT);
                arduino.pinMode(dir_ser, PinMode.OUTPUT);

                arduino.pinMode(11, PinMode.PWM);
                arduino.pinMode(3, PinMode.PWM);
                //arduino.pinMode(6, PinMode.PWM);
                //arduino.pinMode(5, PinMode.PWM);

                enable();
            }));
        }

        byte latchState = 0;

        private void SetDirection(Motor motor, Direction direction)
        {
            MotorLatchBits bit1, bit2;

            switch(motor)
            {
                case Motor.Motor1:
                    bit1 = MotorLatchBits.motor_1a;
                    bit2 = MotorLatchBits.motor_1b;
                    break;
                case Motor.Motor2:
                    bit1 = MotorLatchBits.motor_2a;
                    bit2 = MotorLatchBits.motor_2b;
                    break;
                case Motor.Motor3:
                    bit1 = MotorLatchBits.motor_3a;
                    bit2 = MotorLatchBits.motor_3b;
                    break;
                case Motor.Motor4:
                    bit1 = MotorLatchBits.motor_4a;
                    bit2 = MotorLatchBits.motor_4b;
                    break;
                default:
                    throw new InvalidOperationException("Unknown motor " + motor);
            }

            switch(direction)
            {
                case Direction.Forward:
                    latchState |= (byte)(1 << (byte)bit1);
                    latchState &= (byte)(unchecked(~(1 << (byte)bit2) & 0xff));
                    break;
                case Direction.Backward:
                    latchState &= (byte)(unchecked(~(1 << (byte)bit1) & 0xff));
                    latchState |= (byte)(1 << (byte)bit2);
                    break;
                case Direction.Release:
                    latchState &= (byte)(unchecked(~(1 << (byte)bit1) & 0xff));
                    latchState &= (byte)(unchecked(~(1 << (byte)bit2) & 0xff));
                    break;
                default:
                    throw new InvalidOperationException("Unknown direction " + direction);
            }
        }

        private void enable()
        {
            arduino.digitalWrite(dir_latch, PinState.HIGH);
            arduino.digitalWrite(dir_enable, PinState.HIGH);
            arduino.digitalWrite(dir_clk, PinState.HIGH);
            arduino.digitalWrite(dir_ser, PinState.LOW);

            SendDirection(0);
            arduino.digitalWrite(dir_enable, PinState.LOW);

            SetSpeed(Motor.Motor1, 0);
            SetSpeed(Motor.Motor2, 0);
        }

        private void SendDirection(byte latch)
        {
            arduino.digitalWrite(dir_latch, PinState.LOW);
            arduino.digitalWrite(dir_ser, PinState.LOW);
            for (int i = 7; i >= 0; i--)
            {
                arduino.digitalWrite(dir_clk, PinState.LOW);
                if ((latch & (1 << i)) == (1 << i))
                {
                    arduino.digitalWrite(dir_ser, PinState.HIGH);
                }
                else
                {
                    arduino.digitalWrite(dir_ser, PinState.LOW);
                }
                arduino.digitalWrite(dir_clk, PinState.HIGH);
            }

            arduino.digitalWrite(dir_latch, PinState.HIGH);
        }

        private void SetSpeed(Motor motor, ushort speed)
        {
            arduino.analogWrite((byte)motor, speed);
        }

        enum Direction
        {
            Forward,
            Backward,
            Release,
        }

        /// <summary>
        /// Motor direction latch bits
        /// </summary>
        enum MotorLatchBits
        {
            motor_1a = 2,
            motor_1b = 3,
            motor_2a = 1,
            motor_2b = 4,
            motor_4a = 0,
            motor_4b = 6,
            motor_3a = 5,
            motor_3b = 7,
        }

        /// <summary>
        /// PWM motor pins
        /// </summary>
        enum Motor
        {
            Motor1 = 11,
            Motor2 = 3,
            Motor3 = 6,
            Motor4 = 5,
        }

        // Arduino pin names for interface to 74HCT595 latch
        byte dir_latch = 12;
        byte dir_clk = 4;
        byte dir_enable = 7;
        byte dir_ser = 8;

        private void forward_click(object sender, RoutedEventArgs e)
        {
            SetSpeed(Motor.Motor1, drivespeed);
            SetSpeed(Motor.Motor2, drivespeed);
            SetDirection(Motor.Motor1, Direction.Forward);
            SetDirection(Motor.Motor2, Direction.Forward);
            SendDirection(latchState);

            arduino.digitalWrite(13, PinState.HIGH);

            StartHaltTimer(drivems);
        }

        private void left_click(object sender, RoutedEventArgs e)
        {
            SetSpeed(Motor.Motor1, turnspeed);
            SetSpeed(Motor.Motor2, turnspeed);
            SetDirection(Motor.Motor1, Direction.Forward);
            SetDirection(Motor.Motor2, Direction.Backward);
            SendDirection(latchState);

            arduino.digitalWrite(13, PinState.HIGH);

            StartHaltTimer(turnms);
        }

        private void stop_click(object sender, RoutedEventArgs e)
        {
            SetSpeed(Motor.Motor1, 0);
            SetSpeed(Motor.Motor2, 0);
            SetDirection(Motor.Motor1, Direction.Release);
            SetDirection(Motor.Motor2, Direction.Release);
            SendDirection(this.latchState);

            arduino.digitalWrite(13, PinState.LOW);
        }

        private void right_click(object sender, RoutedEventArgs e)
        {
            SetSpeed(Motor.Motor1, turnspeed);
            SetSpeed(Motor.Motor2, turnspeed);
            SetDirection(Motor.Motor1, Direction.Backward);
            SetDirection(Motor.Motor2, Direction.Forward);
            SendDirection(latchState);

            arduino.digitalWrite(13, PinState.HIGH);

            StartHaltTimer(turnms);
        }

        private void backwards_click(object sender, RoutedEventArgs e)
        {
            SetSpeed(Motor.Motor1, drivespeed);
            SetSpeed(Motor.Motor2, drivespeed);
            SetDirection(Motor.Motor1, Direction.Backward);
            SetDirection(Motor.Motor2, Direction.Backward);
            SendDirection(latchState);

            arduino.digitalWrite(13, PinState.HIGH);

            StartHaltTimer(drivems);
        }

        const byte drivespeed = 255;
        const int drivems = 10000;
        const byte turnspeed = 255;
        const int turnms = 300;

        private DispatcherTimer timer;

        private void StartHaltTimer(int ms)
        {
            if(this.timer != null)
            {
                return;
            }

            this.timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(ms);
            timer.Tick += (s, e) => 
            {
                this.stop_click(null, null);
                this.timer.Stop();
                this.timer = null;
            };
            timer.Start();
        }
    }
}
