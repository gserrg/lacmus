﻿using Avalonia.Media;
using RescuerLaApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace RescuerLaApp.ViewModels
{
    /*TODO: Отрефакторить и разделить класс*/
    public class MainWindowViewModel : ViewModelBase
    {
        #region filds
        
        private double _canvasHeight = 500;
        private double _canvasWidth = 500;
        private int _selectedIndex;
        private List<Frame> _frames = new List<Frame>();
        private List<BoundBox> _boundBoxes = new List<BoundBox>();
        private ImageBrush _imageBrush =  new ImageBrush() { Stretch = Stretch.Uniform };
        private AppStatusInfo _status = new AppStatusInfo() { Status = Enums.Status.Ready };
        private int _frameLoadProgressIndex;
        
        private RelayCommand _openFieCommand;
        private RelayCommand _increaseCanvasCommand;
        private RelayCommand _shrinkCanvasCommand;
        private RelayCommand _nextImageCommand;
        private RelayCommand _prevImageCommand;
        private RelayCommand _predictAllCommand;

        #endregion

        #region properies

        public List<BoundBox> BoundBoxes
        {
            get => _boundBoxes; 
            set => this.RaiseAndSetIfChanged(ref _boundBoxes, value);
        }

        public double CanvasWidth
        {
            get => _canvasWidth;
            set => this.RaiseAndSetIfChanged(ref _canvasWidth, value);
        }

        public double CanvasHeight
        {
            get => _canvasHeight;
            set => this.RaiseAndSetIfChanged(ref _canvasHeight, value);
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedIndex, value);
                
                if(Status.Status == Enums.Status.Ready)
                    Status = new AppStatusInfo()
                    {
                        Status = Enums.Status.Ready, 
                        StringStatus = $"{Enums.Status.Ready.ToString()} | {Frames[SelectedIndex].Patch}"
                    };
                UpdateUi();
            }
        }
        public List<Frame> Frames
        {
            get => _frames;
            set => this.RaiseAndSetIfChanged(ref _frames, value);
        }
        public AppStatusInfo Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }
        public ImageBrush ImageBrush
        {
            get => _imageBrush;
            set => this.RaiseAndSetIfChanged(ref _imageBrush, value);
        }

        #endregion

        #region commands

        public ICommand PredictAllCommand => _predictAllCommand ?? (_predictAllCommand = new RelayCommand(PredictAll));
        public ICommand NextImageCommand => _nextImageCommand ?? (_nextImageCommand = new RelayCommand(NextImage));
        public ICommand PrevImageCommand => _prevImageCommand ?? (_prevImageCommand = new RelayCommand(PrevImage));
        public ICommand ShrinkCanvasCommand => _shrinkCanvasCommand ?? (_shrinkCanvasCommand = new RelayCommand(ShrinkCanvas));
        public ICommand IncreaseCanvasCommand => _increaseCanvasCommand ?? (_increaseCanvasCommand = new RelayCommand(IncreaseCanvas));
        public ICommand OpenFileCommand => _openFieCommand ?? (_openFieCommand = new RelayCommand(OpenFile));

        #endregion

        #region metods

        private async void PredictAll()
        {
            if (_frames == null || _frames.Count < 1) return;
            using (var model = new NeuroModel())
            {
                model.Initialize();
                var index = 0;
                Status = new AppStatusInfo()
                {
                    Status = Enums.Status.Working, 
                    StringStatus = $"Working | processing images: {index} / {Frames.Count}"
                };
                foreach (var frame in _frames)
                {
                    index++;
                    frame.Rectangles = await model.Predict(frame);
                    if(index < Frames.Count)
                        Status = new AppStatusInfo()
                        {
                            Status = Enums.Status.Working, 
                            StringStatus = $"Working | processing images: {index} / {Frames.Count}"
                        };
                    else
                    {
                        Status = new AppStatusInfo()
                        {
                            Status = Enums.Status.Ready
                        };
                    }
                }
            }
            UpdateUi();
        }
        
        private void NextImage()
        {
            if (SelectedIndex < Frames.Count - 1)
                SelectedIndex++;
        }
        private void PrevImage()
        {
            if (SelectedIndex > 0)
                SelectedIndex--;
        }
        
        private void ShrinkCanvas()
        {
            CanvasWidth -= CanvasWidth * 0.25;
            CanvasHeight -= CanvasHeight * 0.25;
            UpdateUi();
        }
        
        private void IncreaseCanvas()
        {
            CanvasWidth += CanvasWidth * 0.25;
            CanvasHeight += CanvasHeight * 0.25;
            UpdateUi();
        }

        
        private async void OpenFile()
        {
            Status = new AppStatusInfo() {Status = Enums.Status.Working};
            try
            {
                var openDig = new OpenFolderDialog()
                {
                    Title = "Choose a directory with images"
                };
                var dirName = await openDig.ShowAsync(new Window());
                if (string.IsNullOrEmpty(dirName))
                {
                    Status = new AppStatusInfo() {Status = Enums.Status.Ready};
                    return;
                }
                var fileNames = Directory.GetFiles(dirName);
                _frameLoadProgressIndex = 0;
                _frames = new List<Frame>();
                foreach (var fileName in fileNames)
                {
                    var frame = new Frame();
                    frame.onLoad += FrameLoadingProgressUpdate;
                    frame.Load(fileName, Enums.ImageLoadMode.Miniature);
                    _frames.Add(frame);
                }
                Frames = new List<Frame>(_frames);
                if (SelectedIndex < 0)
                    SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Status = new AppStatusInfo()
                {
                    Status = Enums.Status.Error, 
                    StringStatus = $"Error | {ex.Message.Replace('\n', ' ')}"
                };
            }
        }

        private void FrameLoadingProgressUpdate()
        {
            _frameLoadProgressIndex++;
            if(_frameLoadProgressIndex < Frames.Count)
                Status = new AppStatusInfo()
                {
                    Status = Enums.Status.Working, 
                    StringStatus = $"Working | loading images: {_frameLoadProgressIndex} / {Frames.Count}"
                };
            else
            {
                Status = new AppStatusInfo()
                {
                    Status = Enums.Status.Ready
                };
            }
        }

        private void UpdateUi()
        {
            /*TODO: Вынести сюда все функции обновления UI*/
            ImageBrush.Source = new Bitmap(Frames[SelectedIndex].Patch); //replace to frame.load(...)
            CanvasHeight = CanvasWidth * ImageBrush.Source.PixelSize.Height / ImageBrush.Source.PixelSize.Width;
            //Frames[SelectedIndex].Resize(CanvasWidth, CanvasHeight);
            if (Frames[SelectedIndex].Rectangles != null && Frames[SelectedIndex].Rectangles.Count > 0)
            {
                var scaleX = CanvasWidth / ImageBrush.Source.PixelSize.Width;
                var scaleY = CanvasHeight / ImageBrush.Source.PixelSize.Height;
                Console.WriteLine($"{ImageBrush.Source.PixelSize.Width} x {ImageBrush.Source.PixelSize.Height}");
                Console.WriteLine($"{CanvasWidth} x {CanvasHeight}");
                foreach (var box in Frames[SelectedIndex].Rectangles)
                {
                    box.Update(scaleX, scaleY);
                }
                BoundBoxes = new List<BoundBox>(Frames[SelectedIndex].Rectangles);
            }
            else
            {
                BoundBoxes = null;
            }    
        }

        #endregion      
    }
}