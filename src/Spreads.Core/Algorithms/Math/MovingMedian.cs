// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Algorithms.Math {
    // implementation of LIGO's Efficient Algorithm for computing a Running Median
    // https://dcc.ligo.org/public/0027/T030168/000/T030168-00.pdf

    public class MovingMedian {
        private readonly Comparer<double> _comparer;
        private readonly Comparer<node> _node_comparer;
        private readonly Comparer<rngmed_val_index> _val_index_comparer;

        public MovingMedian(Comparer<double> comparer) {
            _comparer = comparer;
            _node_comparer = new NodeComparer(Comparer<double>.Default);
            _val_index_comparer = new rngmed_val_index_Comparer(Comparer<double>.Default);
        }

        struct node {
            public double data;
            public int next_sorted;
            public int next_sequence;
            public int prev_sorted;
        }

        struct rngmed_val_index {
            public double data;
            public int index;
        }

        class NodeComparer : Comparer<node> {
            private readonly Comparer<double> _comparer;

            public NodeComparer(Comparer<double> comparer) {
                _comparer = comparer;
            }

            public override int Compare(node x, node y) {
                return _comparer.Compare(x.data, y.data);
            }
        }

        class rngmed_val_index_Comparer : Comparer<rngmed_val_index> {
            private readonly Comparer<double> _comparer;

            public rngmed_val_index_Comparer(Comparer<double> comparer) {
                _comparer = comparer;
            }

            public override int Compare(rngmed_val_index x, rngmed_val_index y) {
                return _comparer.Compare(x.data, y.data);
            }
        }

        /*-------------------------------
            checks: pointers to subset of nodes to use
            as checkpoints 
            ---------------------------------*/

        int[] sorted_indices;
        rngmed_val_index[] index_block;
        node[] node_addresses;
        int[] checks;
        int first_sequence_idx, last_sequence_idx;
        int currentnode_idx = -1, previousnode_idx = -1;
        int leftnode_idx = -1, rightnode_idx = -1;
        int reuse_next_sorted_idx = -1, reuse_prev_sorted_idx = -1;
        int dummy_node_idx, dummy_node1_idx, dummy_node2_idx;
        int ncheckpts, stepchkpts;
        int nextchkptindx;
        int[] checks4shift;
        int nearestchk, midpoint, offset, numberoffsets;
        int samplecount, k, counter_chkpt, chkcount = 0, shiftcounter = 0;
        double nextsample, deletesample, dummy;
        int shift, dummy_int;


        public int rngmed(double[] data, int nblocks, double[] medians) {


            /*-----------------------------------
              Sort the first block of nblocks samples
              using the qsort function
            ------------------------------------*/
            index_block = new rngmed_val_index[nblocks];
            for (k = 0; k < (int)nblocks; k++) {
                index_block[k].data = data[k];
                index_block[k].index = k;
            }

            Array.Sort<rngmed_val_index>(index_block, 0, nblocks, _val_index_comparer);

            sorted_indices = new int[nblocks];
            for (k = 0; k < (int)nblocks; k++) {
                sorted_indices[k] = index_block[k].index;
            }

            index_block = null;



            ///*----------------------------------
            //Indices of checkpoint nodes.
            //Number of nodes per checkpoint=floor(sqrt(nblocks))
            //------------------------------------*/
            stepchkpts = (int)System.Math.Sqrt(nblocks);
            ncheckpts = nblocks / stepchkpts;
            checks = new int[ncheckpts]; // (struct node **)LALCalloc(ncheckpts,sizeof(struct node*));
            checks4shift = new int[ncheckpts];



            ///*---------------------------------
            //  Offsets for getting median from nearest
            //  checkpoint: For nblocks even, 
            //  (node(offset(1))+node(offset(2)))/2;
            //  for nblocks odd,
            //  (node(offset(1))+node(offset(1)))/2;
            //  THIS CAN BE OPTIMISED.
            // ----------------------------------*/
            if (nblocks % 2 == 1) { // (int)System.Math.IEEERemainder(()nblocks, 2.0)) {
                /*Odd*/
                midpoint = (nblocks + 1) / 2 - 1;
                numberoffsets = 1;
            } else {
                /*Even*/
                midpoint = nblocks / 2 - 1;
                numberoffsets = 2;
            }
            nearestchk = midpoint / stepchkpts; // floor(midpoint / stepchkpts);
            offset = midpoint - nearestchk * stepchkpts;




            /*----------------------------------
            Build up linked list using first nblock points
            in sequential order
            ------------------------------------*/
            node_addresses = new node[nblocks]; // (struct node **)LALCalloc(nblocks,sizeof(struct node *));

            //var first_sequence = new node(); // (struct node *)LALCalloc(1,sizeof(struct node));
            node_addresses[0] = new node();

            first_sequence_idx = 0;
            node_addresses[0].next_sequence = -1;
            node_addresses[0].next_sorted = -1;
            node_addresses[0].prev_sorted = -1;
            node_addresses[0].data = data[0];

            previousnode_idx = first_sequence_idx;

            //var previousnode = first_sequence;

            for (samplecount = 1; samplecount < (int)nblocks; samplecount++) {
                var currentnode = new node();//  (struct node *)LALCalloc(1,sizeof(struct node));
                currentnode.next_sequence = -1;
                currentnode.prev_sorted = -1;
                currentnode.next_sorted = -1;
                currentnode.data = data[samplecount];

                node_addresses[samplecount] = currentnode;

                node_addresses[previousnode_idx].next_sequence = samplecount;
                // update previous node in array
                //node_addresses[samplecount - 1].next_sequence = previousnode;
                previousnode_idx = samplecount;
            }
            last_sequence_idx = samplecount - 1;


            /*------------------------------------
            Set the sorted sequence pointers and
            the pointers to checkpoint nodes
            -------------------------------------*/
            currentnode_idx = sorted_indices[0];
            previousnode_idx = -1;
            checks[0] = currentnode_idx;
            nextchkptindx = stepchkpts;
            counter_chkpt = 1;
            for (samplecount = 1; samplecount < (int)nblocks; samplecount++) {
                dummy_node_idx = sorted_indices[samplecount];
                node_addresses[currentnode_idx].next_sorted = dummy_node_idx;
                node_addresses[currentnode_idx].prev_sorted = previousnode_idx;
                previousnode_idx = currentnode_idx;
                currentnode_idx = dummy_node_idx;
                if (samplecount == nextchkptindx && counter_chkpt < ncheckpts) {
                    checks[counter_chkpt] = currentnode_idx;
                    nextchkptindx += stepchkpts;
                    counter_chkpt++;
                }
            }
            node_addresses[currentnode_idx].prev_sorted = previousnode_idx;
            node_addresses[currentnode_idx].next_sorted = -1;
            sorted_indices = null;


            /*------------------------------
              Get the first output element
            -------------------------------*/
            if (medians == null) {
                throw new ArgumentNullException(nameof(medians));
            }
            currentnode_idx = checks[nearestchk];
            for (k = 1; k <= offset; k++) {
                currentnode_idx = node_addresses[currentnode_idx].next_sorted;
            }
            dummy = 0;
            for (k = 1; k <= numberoffsets; k++) {
                dummy += node_addresses[currentnode_idx].data;
                currentnode_idx = node_addresses[currentnode_idx].next_sorted;
            }
            medians[0] = dummy / numberoffsets;


            /*---------------------------------
            This is the main part.
            Find the nodes whose values
            form the smallest closed interval
            around the new incoming value.
            The right limit is always >
            the new value.
            ----------------------------------*/
            for (samplecount = nblocks; samplecount < (int)data.Length; samplecount++) {
                nextsample = data[samplecount];
                if (nextsample >= node_addresses[checks[0]].data) {
                    for (chkcount = 1; chkcount < ncheckpts; chkcount++) {
                        if (nextsample >= node_addresses[checks[chkcount]].data) {
                        } else {
                            break;
                        }
                    }
                    chkcount -= 1;
                    rightnode_idx = checks[chkcount];
                    leftnode_idx = -1; /*NEW*/
                    // NB originally it was a pointer, so we need to check for >=0 for index in array
                    // because pointer to the zero addressed node would evaluate to true in C
                    while (rightnode_idx >= 0) {
                        if (nextsample < node_addresses[rightnode_idx].data) {
                            break;
                        }
                        leftnode_idx = rightnode_idx;
                        rightnode_idx = node_addresses[rightnode_idx].next_sorted;
                    }

                } else {
                    if (nextsample < node_addresses[checks[0]].data) {
                        chkcount = 0;
                        /* dummy_node=checks[0]; */
                        rightnode_idx = checks[0];
                        leftnode_idx = -1;
                    }
                }


                /*-------------------------
                     Determine if checkpoints need to be 
                     shifted or not.
                   ---------------------------*/
                dummy_node_idx = -1;
                if (rightnode_idx == first_sequence_idx) {
                    dummy_node_idx = rightnode_idx;
                } else if (leftnode_idx == first_sequence_idx) {
                    dummy_node_idx = leftnode_idx;
                }
                if (dummy_node_idx >= 0) {
                    node_addresses[dummy_node_idx].data = nextsample;
                    first_sequence_idx = node_addresses[first_sequence_idx].next_sequence;
                    node_addresses[dummy_node_idx].next_sequence = -1;
                    node_addresses[last_sequence_idx].next_sequence = dummy_node_idx;
                    last_sequence_idx = dummy_node_idx;
                    shift = 0;
                } else {
                    reuse_next_sorted_idx = rightnode_idx;
                    reuse_prev_sorted_idx = leftnode_idx;
                    shift = 1; /*shift maybe required*/
                }


                /*-----------------------------------
                   Getting check points to be shifted
                 -----------------------------------*/
                // NB shift was originally int, not note*, so GT, not GE
                if (shift > 0) {
                    deletesample = node_addresses[first_sequence_idx].data;
                    if (deletesample > nextsample) {
                        shiftcounter = 0;
                        for (k = chkcount; k < ncheckpts; k++) {
                            dummy = node_addresses[checks[k]].data;
                            if (dummy > nextsample) {
                                if (dummy <= deletesample) {
                                    checks4shift[shiftcounter] = k;
                                    shiftcounter++;
                                } else {
                                    break;
                                }
                            }
                        }
                        shift = -1; /*Left shift*/
                    } else
                          if (deletesample <= nextsample) {
                        shiftcounter = 0;
                        for (k = chkcount; k >= 0; k--) {
                            dummy = node_addresses[checks[k]].data;
                            if (dummy >= deletesample) {
                                checks4shift[shiftcounter] = k;
                                shiftcounter++;
                            } else {
                                break;
                            }
                        }
                        shift = 1; /*Shift Right*/
                    }
                }


                /*------------------------------
                 Recycle the node with the 
                 oldest value. 
                --------------------------------*/
                if (shift > 0) {
                    /*---------------------
                     Reset sequential links
                     ---------------------*/
                    dummy_node_idx = first_sequence_idx;
                    first_sequence_idx = node_addresses[dummy_node_idx].next_sequence;
                    node_addresses[dummy_node_idx].next_sequence = -1;
                    node_addresses[last_sequence_idx].next_sequence = dummy_node_idx;
                    last_sequence_idx = dummy_node_idx;
                    node_addresses[dummy_node_idx].data = nextsample;
                    dummy_node1_idx = node_addresses[dummy_node_idx].prev_sorted;
                    dummy_node2_idx = node_addresses[dummy_node_idx].next_sorted;
                    /*-----------------------
                      Repair deletion point
                    ------------------------*/
                    if (dummy_node1_idx == -1) {
                        node_addresses[dummy_node2_idx].prev_sorted = dummy_node1_idx;
                    } else {
                        if (dummy_node2_idx == -1) {
                            node_addresses[dummy_node1_idx].next_sorted = dummy_node2_idx;
                        } else {
                            node_addresses[dummy_node1_idx].next_sorted = dummy_node2_idx;
                            node_addresses[dummy_node2_idx].prev_sorted = dummy_node1_idx;
                        }
                    }
                    /*------------------------
                      Set pointers from neighbours to new node at insertion point
                    -------------------------*/
                    if (rightnode_idx == -1) {
                        node_addresses[leftnode_idx].next_sorted = dummy_node_idx;
                    } else {
                        if (leftnode_idx == -1) {
                            node_addresses[rightnode_idx].prev_sorted = dummy_node_idx;
                        } else {
                            node_addresses[leftnode_idx].next_sorted = dummy_node_idx;
                            node_addresses[rightnode_idx].prev_sorted = dummy_node_idx;
                        }
                    }

                    /*-------------------------------
                      Shift check points before resetting sorted list
                    --------------------------------*/
                    if (shift == -1) {
                        for (k = 0; k < shiftcounter; k++) {
                            dummy_int = checks4shift[k];
                            checks[dummy_int] = node_addresses[checks[dummy_int]].prev_sorted;
                        }
                    } else
                           if (shift == 1) {
                        for (k = 0; k < shiftcounter; k++) {
                            dummy_int = checks4shift[k];
                            checks[dummy_int] = node_addresses[checks[dummy_int]].next_sorted;
                        }
                    }

                    /*--------------------------------
                      insert node
                     --------------------------------*/
                    node_addresses[dummy_node_idx].next_sorted = reuse_next_sorted_idx;
                    node_addresses[dummy_node_idx].prev_sorted = reuse_prev_sorted_idx;
                }



                /*--------------------------------
                  Get the median
                ---------------------------------*/
                currentnode_idx = checks[nearestchk];
                for (k = 1; k <= offset; k++) {
                    currentnode_idx = node_addresses[currentnode_idx].next_sorted;
                }
                dummy = 0;
                for (k = 1; k <= numberoffsets; k++) {
                    dummy += node_addresses[currentnode_idx].data;
                    currentnode_idx = node_addresses[currentnode_idx].next_sorted;
                }
                medians[samplecount - nblocks + 1] = dummy / numberoffsets;

            }/*Outer For Loop*/
            return 0;
        }


    }
}
