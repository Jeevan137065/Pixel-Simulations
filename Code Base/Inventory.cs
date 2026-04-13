using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
{
    public class Inventory
    {
        public const int TotalSlots = 30;
        public const int HotbarSlots = 10;
        public const int MaxStackSize = 999;

        public ItemStack[] Slots { get; private set; } = new ItemStack[TotalSlots];
        public ItemStack HeldItem { get; set; } // The item currently picked up by the mouse
        public int SelectedSlot { get; private set; } = 0;

        public float MaxWeight { get; set; } = 50.0f; // Limit in kg
        public float CurrentWeight { get; private set; } = 0.0f;

        // Customizable visual properties
        public Color SlotColor { get; set; } = Color.Black * 0.6f;
        public Color BorderColor { get; set; } = Color.White;
        public Color HighlightColor { get; set; } = Color.Yellow;
        public float BorderThickness { get; set; } = 2f;

        public void SelectSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < HotbarSlots) SelectedSlot = slotIndex;
        }

        public ItemStack GetSelectedItem() => Slots[SelectedSlot];

        /// <summary>
        /// Adds items to the inventory, generating a random weight for each piece.
        /// Returns the number of items that COULD NOT be added (due to weight or slot limits).
        /// </summary>
        public int AddItem(int id, int amount, ItemManager manager)
        {
            var def = manager.GetItem(id);
            if (def == null) return amount;

            while (amount > 0)
            {
                float itemWeight = manager.GetRandomWeight(def);

                // Stop if adding this exact item pushes us over the weight limit
                if (CurrentWeight + itemWeight > MaxWeight) break;

                int targetSlot = -1;

                // 1. Try to find an existing stack that isn't full
                for (int i = 0; i < TotalSlots; i++)
                {
                    if (Slots[i] != null && Slots[i].ItemID == id && Slots[i].Count < MaxStackSize)
                    {
                        targetSlot = i; break;
                    }
                }

                // 2. If no stack available, find an empty slot
                if (targetSlot == -1)
                {
                    for (int i = 0; i < TotalSlots; i++)
                    {
                        if (Slots[i] == null)
                        {
                            targetSlot = i;
                            Slots[i] = new ItemStack { ItemID = id, Count = 0, TotalWeight = 0 };
                            break;
                        }
                    }
                }

                // If inventory is entirely full of different items/maxed stacks
                if (targetSlot == -1) break;

                // Safely add the single item to the slot and inventory totals
                Slots[targetSlot].Count++;
                Slots[targetSlot].TotalWeight += itemWeight;
                CurrentWeight += itemWeight;
                amount--;
            }

            return amount;
        }
        public void HandleMouseInteraction(Point mousePos, bool isLeftClick, Vector2 screenSize,bool isInventoryOpen)
        {
            if (!isLeftClick) return;

            int scaleFactor = 2;
            int slotSize = 48 * scaleFactor;
            int spacing = 4 * scaleFactor;
            int startX = (int)(screenSize.X - (10 * slotSize + 9 * spacing)) / 2;

            if (isInventoryOpen)
            {
                int fullInvY = (int)(screenSize.Y - (3 * slotSize + 2 * spacing)) / 2;

                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 10; col++)
                    {
                        int x = startX + col * (slotSize + spacing);
                        int y = fullInvY + row * (slotSize + spacing);
                        Rectangle rect = new Rectangle(x, y, slotSize, slotSize);

                        if (rect.Contains(mousePos))
                        {
                            int index = row * 10 + col;

                            if (HeldItem != null && Slots[index] != null && HeldItem.ItemID == Slots[index].ItemID)
                            {
                                // Merge Stacks
                                int spaceLeft = MaxStackSize - Slots[index].Count;
                                int transfer = Math.Min(spaceLeft, HeldItem.Count);

                                float avgWeight = HeldItem.TotalWeight / HeldItem.Count;
                                float weightTransferred = avgWeight * transfer;

                                Slots[index].Count += transfer;
                                Slots[index].TotalWeight += weightTransferred;

                                HeldItem.Count -= transfer;
                                HeldItem.TotalWeight -= weightTransferred;

                                if (HeldItem.Count <= 0) HeldItem = null;
                            }
                            else
                            {
                                // Swap Stacks
                                var temp = Slots[index];
                                Slots[index] = HeldItem;
                                HeldItem = temp;
                            }
                            return;
                        }
                    }
                }
            }
            else
            {
                // Check Hotbar Clicks when inventory is closed
                int hotbarY = (int)(screenSize.Y - slotSize - 20);
                for (int col = 0; col < 10; col++)
                {
                    int x = startX + col * (slotSize + spacing);
                    Rectangle rect = new Rectangle(x, hotbarY, slotSize, slotSize);

                    if (rect.Contains(mousePos))
                    {
                        SelectSlot(col);
                        return;
                    }
                }
            }
        }
    }
}
