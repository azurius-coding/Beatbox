   M S B A M L   `   `   `  ����   Beatbox     Beatbox.NotificationWindow   \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     Etelerik1http://schemas.telerik.com/2008/xaml/presentation     R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     -     � Title$  NotificationWindow�� � SizeToContent$ WidthAndHeight=� � ShowInTaskbar$ False�� � WindowStyle$
 None=� � AllowsTransparency$
 True��
������ .���  J� RenderTransformOrigin$	 0,1&�.+��� $��1q�
�������
������� �� CornerRadius$ 7��._�� -    �Name$
 grid��$��	10,5,10,5q�.+��� $��Center=�$��Center=�.+��� -    ��Name$ title���� -   	 ��Name$	 	separator��$��1��
�������$��0,5,0,5q����� 
 �� ElementName$

 grid�� �� Path$ ActualWidth��� -   $ content�� � Triggers :�  :� RoutedEvent$ FrameworkElement.Loaded��.[��� -    ��Name$ StoryboardLoad��.���� .��W�  �� TargetProperty$ (UIElement.Opacity)� W� From 0.0	 W� To 1.0 g� Duration$ 0:0:2D�W� -   $ (UIElement.Opacity)� 1.0 0.0$ 0:0:8D� g� 	BeginTime	 0:0:5:� $ Mouse.MouseEnter��[���  �� BeginStoryboardName$ StoryboardLoad���� $ StoryboardFade��:� $ Mouse.MouseLeave��.[��� -   $ StoryboardFade��.���� .��W� -	   $ (UIElement.Opacity)� 1.0 0.0$ 0:0:8D�	 0:0:2z���  �� ScaleY$ 1Q�