

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Xml.Schema;
using ImGuiNET;
using Pulsar4X.ECSLib;

namespace Pulsar4X.SDL2UI
{
    public class ResearchWindow : PulsarGuiWindow
    {
        private FactionTechDB _factionTechDB;
        private Dictionary<Guid, (TechSD tech, int amountDone, int amountMax)> _researchableTechsByGuid;
        private List<(TechSD tech, int amountDone, int amountMax)> _researchableTechs;
        
        private EntityState _currentEntity;
        private List<(Scientist scientist, Entity atEntity)> _scienceTeams;
        private int _selectedTeam = -1;
       
        private ResearchWindow()
        {
            OnFactionChange();
            _state.Game.GameLoop.GameGlobalDateChangedEvent += GameLoopOnGameGlobalDateChangedEvent; 
        }

        private void GameLoopOnGameGlobalDateChangedEvent(DateTime newdate)
        {
            if (IsActive)
            {
                _researchableTechs = _factionTechDB.GetResearchableTechs();
                _researchableTechsByGuid = _factionTechDB.GetResearchablesDic();
            }
        }


        internal static ResearchWindow GetInstance()
        {
            ResearchWindow thisitem;
            if (!_state.LoadedWindows.ContainsKey(typeof(ResearchWindow)))
            {
                thisitem = new ResearchWindow();
            }
            thisitem = (ResearchWindow)_state.LoadedWindows[typeof(ResearchWindow)];
            if (_state.LastClickedEntity != thisitem._currentEntity)
            {
                if (_state.LastClickedEntity.Entity.HasDataBlob<TeamsHousedDB>())
                {
                    thisitem.OnEntityChange(_state.LastClickedEntity);
                }
            }


            return thisitem;
        }


        private void OnFactionChange()
        {
            _factionTechDB = _state.Faction.GetDataBlob<FactionTechDB>();
            _researchableTechs = _factionTechDB.GetResearchableTechs();
            _researchableTechsByGuid = _factionTechDB.GetResearchablesDic();
            _scienceTeams = _factionTechDB.AllScientists;
        }

 

        private void OnEntityChange(EntityState entityState)
        {
            _currentEntity = entityState;
        }


        internal override void Display()
        {
            if (IsActive && ImGui.Begin("Research and Development", ref IsActive, _flags))
            {
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 300);
                ImGui.Text("Projects");
                ImGui.NextColumn();
                ImGui.Text("Science Teams");
                ImGui.NextColumn();
                ImGui.Separator();
                
                ImGui.BeginChild("ResearchablesHeader", new Vector2(300, ImGui.GetTextLineHeightWithSpacing() + 2));
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 250);
                ImGui.Text("Tech");
                ImGui.NextColumn();
                ImGui.Text("Level");
                ImGui.NextColumn();
                ImGui.Separator();
                ImGui.EndChild();
                
                ImGui.BeginChild("techlist", new Vector2(300, 250));
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0,250);
                
                for (int i = 0; i < _researchableTechs.Count; i++)
                {
                    if (_researchableTechs[i].amountMax > 0) //could happen if bad json data?
                    {
                        float frac = (float)_researchableTechs[i].amountDone / _researchableTechs[i].amountMax;
                        var size = ImGui.GetTextLineHeight();
                        var pos = ImGui.GetCursorPos();
                        ImGui.ProgressBar(frac, new Vector2(248, size), "");
                        ImGui.SetCursorPos(pos);
                        ImGui.Text(_researchableTechs[i].tech.Name);
                        
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
                        {
                            if(_selectedTeam > -1)
                                ResearchProcessor.AssignProject(_scienceTeams[_selectedTeam].scientist, _researchableTechs[i].tech.ID);
                        }
                        if(ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(_researchableTechs[i].tech.Description);
                        }
                        ImGui.NextColumn();
                        ImGui.Text(_factionTechDB.GetLevelforTech(_researchableTechs[i].tech).ToString());
                        
                        ImGui.NextColumn();
                    }
                }
                ImGui.EndChild();
                
                ImGui.NextColumn();

                ImGui.BeginChild("Teams", new Vector2(550, 250));
                
                ImGui.Columns(4);
                ImGui.SetColumnWidth(0, 150);
                ImGui.SetColumnWidth(1, 150);
                ImGui.SetColumnWidth(2, 100);
                ImGui.SetColumnWidth(3, 150);
                ImGui.Text("Scientist");
                ImGui.NextColumn();
                ImGui.Text("Location");
                ImGui.NextColumn();
                ImGui.Text("Labs");
                ImGui.NextColumn();
                ImGui.Text("Current Project");
                ImGui.NextColumn();
                ImGui.Separator();
                for (int i = 0; i < _scienceTeams.Count; i++)
                {

                    bool isSelected = _selectedTeam == i;
                    
                    Scientist scint = _scienceTeams[i].scientist;
                    if (ImGui.Selectable(_scienceTeams[i].Item1.Name, isSelected))
                    {
                        _selectedTeam = i;
                    }

                    ImGui.NextColumn();
                    ImGui.Text(_scienceTeams[i].atEntity.GetDataBlob<NameDB>().GetName(_state.Faction));
                    
                    ImGui.NextColumn();
                    int allfacs = 0;
                    int facsAssigned = scint.AssignedLabs;
                    //int facsFree = 0;
                    if(
                    _scienceTeams[i].atEntity.GetDataBlob<ComponentInstancesDB>().TryGetComponentsByAttribute<ResearchPointsAtbDB>( out var foo ))
                    {
                        allfacs = foo.Count;
                        //facsFree = allfacs - facsAssigned;
                    }
                    ImGui.Text(facsAssigned.ToString() + "/" + allfacs.ToString());
                    if(ImGui.IsItemHovered())
                        ImGui.SetTooltip("Assigned / Total");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("+"))
                    {
                        ResearchProcessor.AddLabs(scint, 1);
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("-"))
                    {
                        ResearchProcessor.AddLabs(scint, -1);
                    }

                    ImGui.NextColumn();
                    if (scint.ProjectQueue.Count > 0 && _factionTechDB.IsResearchable(scint.ProjectQueue[0].techID))
                    {
                        var proj = _researchableTechsByGuid[scint.ProjectQueue[0].techID];
                        
                        float frac = (float)proj.amountDone / proj.amountMax;
                        var size = ImGui.GetTextLineHeight();
                        var pos = ImGui.GetCursorPos();
                        ImGui.ProgressBar(frac, new Vector2(150, size), "");
                        ImGui.SetCursorPos(pos);
                        ImGui.Text(proj.tech.Name);
                        if(ImGui.IsItemHovered())
                        {
                            string queue = "";
                            foreach (var queueItem in _scienceTeams[i].scientist.ProjectQueue)
                            {
                                queue += _researchableTechsByGuid[queueItem.techID].tech.Name + "\n";
                            }
                            ImGui.SetTooltip(queue);
                        }



                    }
                }
                ImGui.EndChild();
                ImGui.Separator();
                ImGui.Columns(1);
                if (_selectedTeam > -1)
                {
                    SelectedSci(_selectedTeam);
                }
            }
        }

        private int hoveredi = -1;
        private void SelectedSci(int selected)
        {
            ImGui.BeginChild("SelectedSci");
            Scientist scientist = _scienceTeams[selected].scientist;

            //ImGui.Columns(2);
            //ImGui.SetColumnWidth(0, 300);
            //ImGui.SetColumnWidth(1, 150);
            
            int loopto = scientist.ProjectQueue.Count;
            if (hoveredi >= scientist.ProjectQueue.Count)
                hoveredi = -1;
            if (hoveredi > -1)
                loopto = hoveredi;

            
            float heightt = ImGui.GetTextLineHeightWithSpacing() * loopto;
            
            var spacingH = ImGui.GetTextLineHeightWithSpacing() - ImGui.GetTextLineHeight();
            
            float hoverHeigt = ImGui.GetTextLineHeightWithSpacing() + spacingH * 3;
            
            float heightb = ImGui.GetTextLineHeightWithSpacing() * scientist.ProjectQueue.Count - loopto;
            float colomnWidth0 = 300;
            
            for (int i = 0; i < loopto; i++)
            {
                ImGui.BeginChild("Top", new Vector2(400, heightt));
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 300);
                (Guid techID, bool cycle) queueItem = _scienceTeams[selected].scientist.ProjectQueue[i];
                (TechSD tech, int amountDone, int amountMax) projItem = _researchableTechsByGuid[queueItem.techID];
                
                ImGui.BeginGroup();
                var cpos = ImGui.GetCursorPos();
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ChildBg));
                ImGui.Button("##projItem.tech.Name", new Vector2(colomnWidth0 - spacingH, ImGui.GetTextLineHeightWithSpacing()));
                ImGui.PopStyleColor();
                ImGui.SetCursorPos(cpos);
                ImGui.Text(projItem.tech.Name);
                ImGui.EndGroup();
                
                if (ImGui.IsItemHovered())
                {
                    hoveredi = i;
                }
                ImGui.NextColumn();
                ImGui.NextColumn();
                
                
                ImGui.EndChild();
            }


            if (hoveredi > -1)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0.5f);
                ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 2f);
                ImGui.BeginChild("Buttons", new Vector2(400, hoverHeigt), true);
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 300);

                (Guid techID, bool cycle) queueItem = _scienceTeams[selected].scientist.ProjectQueue[hoveredi];
                (TechSD tech, int amountDone, int amountMax) projItem = _researchableTechsByGuid[queueItem.techID];


                ImGui.BeginGroup();
                ImGui.Text(projItem.tech.Name);
                ImGui.EndGroup();
                
                ImGui.NextColumn();
                
                Buttons(scientist, queueItem, hoveredi);
                
                ImGui.NextColumn();
                
                ImGui.EndChild();
                ImGui.PopStyleVar(2);


                for (int i = hoveredi + 1; i < scientist.ProjectQueue.Count; i++)
                {
                    ImGui.BeginChild("Bottom", new Vector2(400, heightb));
                    ImGui.Columns(2);
                    ImGui.SetColumnWidth(0, 300);
                    (Guid techID, bool cycle) queueItem1 = _scienceTeams[selected].scientist.ProjectQueue[i];
                    (TechSD tech, int amountDone, int amountMax) projItem1 = _researchableTechsByGuid[queueItem1.techID];
                    
                    ImGui.BeginGroup();
                    var cpos = ImGui.GetCursorPos();
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ChildBg));
                    ImGui.Button("##projItem1.tech.Name", new Vector2(colomnWidth0 - spacingH, ImGui.GetTextLineHeightWithSpacing()));
                    ImGui.PopStyleColor();
                    ImGui.SetCursorPos(cpos);
                    ImGui.Text(projItem1.tech.Name);
                    ImGui.EndGroup();

                    if (ImGui.IsItemHovered())
                    {
                        hoveredi = i;
                    }
                    
                    ImGui.NextColumn();
                    ImGui.NextColumn();

                    ImGui.EndChild();
                }
            }

            /*
            for (int i = 0; i < scientist.ProjectQueue.Count; i++)
            {
                ImGui.BeginChild("Top");
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 300);
                (Guid techID, bool cycle) queueItem = _scienceTeams[selected].scientist.ProjectQueue[i];
                (TechSD tech, int amountDone, int amountMax) projItem = _researchableTechsByGuid[queueItem.techID];
                
                
                ImGui.Text(projItem.tech.Name);
                if (ImGui.IsItemHovered())
                {
                    hoveredi = i;
                }
                //ImGui.Text(proj.Description);
                //ImGui.NextColumn();
                //ImGui.SameLine();
                //ImGui.Text(projItem.tech.Category.ToString());

                ImGui.EndChild();
                if (i == hoveredi)
                {
                    ImGui.BeginChild("Buttons");
                    ImGui.Columns(2);
                    ImGui.SetColumnWidth(0, 300);
                    Buttons(scientist, queueItem, i);
                    ImGui.EndChild();
                }

                ImGui.BeginChild("Bottom");

                
                
                
                
                ImGui.EndChild();
                
                
                if (i != hoveredi) //if it's not hovered, make it invisible. 
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0,0,0,0));
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0,0,0,0));
                    Buttons(scientist, queueItem, i);
                    ImGui.PopStyleColor(2);
                }
                else
                    Buttons(scientist, queueItem, i);
                
                ImGui.NextColumn();
                */
            
            ImGui.EndChild();

        }

        void Buttons(Scientist scientist, (Guid techID, bool cycle) queueItem, int i)
        {
            
            ImGui.BeginGroup();
            string cyclestr = "*";
            if (queueItem.cycle)
                cyclestr = "O";
            if (ImGui.SmallButton(cyclestr + "##" + i))
            {
                scientist.ProjectQueue[i] = (queueItem.techID, !queueItem.cycle);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Requeue Project");

            ImGui.SameLine();
            if (ImGui.SmallButton("^" + "##" + i) && i > 0)
            {
                scientist.ProjectQueue.RemoveAt(i);
                scientist.ProjectQueue.Insert(i - 1, queueItem);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("v" + "##" + i) && i < scientist.ProjectQueue.Count - 1)
            {

                scientist.ProjectQueue.RemoveAt(i);
                scientist.ProjectQueue.Insert(i + 1, queueItem);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("x" + "##" + i))
            {
                scientist.ProjectQueue.RemoveAt(i);
            }
                
            ImGui.EndGroup();
            if (ImGui.IsItemHovered())
            {
                hoveredi = i;
            }
        }

    }
}