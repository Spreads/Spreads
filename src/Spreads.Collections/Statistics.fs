// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


module Angara.Statistics

//
// Statistical library
//

type Distribution = 
  /// A uniform distribution over [`lower_bound`, `upper_bound`) range.
  | Uniform of float*float // lower bound, upper bound (> lower bound) -> continuous [lower bound to upper bound)
  /// A uniform distribution in log space.
  | LogUniform of float*float // lower bound (> 0), upper bound (> lower bound) -> continuous [lower bound to upper bound)
  /// A linearly changing distribution over a [`lower_bound`, `upper_bound`) range.
  | Linear of lower_bound:float * upper_bound:float * density_at_lower_bound:float
  /// Normal distribution of `mean`, `standard_deviation`.
  | Normal of float*float // mean, standard deviation (> 0) -> continuous (-infinity to infinity)
  /// Normal distribution in log space of `mean`, `standard_deviation_of_log`.
  | LogNormal of float*float // log mean, standard deviation of logarithm (> 0) -> continuous (0.0 to infinity)
  /// Distribution of a yes/no experiment (1 or 0) which yields success with probability `p`.
  | Bernoulli of float // fraction of success [1e-16 to 1.0-1e-16] -> success/failure outcome, 1 or 0
  /// A number of successes in a sequence of `n` independent yes/no experiments, each of which yields success with probability `p`.
  | Binomial of int*float // number of trials, probability of success -> number of successes, [0 to max_int]
  /// A number of successes before a given number of failures `r` in a sequence of yes/no experiments, each of which yields success with probability `p = mean/(mean+r)`.
  | NegativeBinomial of mean:float * r:float // mean (0 to inf), number of failures or 'shape' for fractional values (0 to inf) -> number of successes, [0 to max_int]
  /// A number of events occuring  in a fixed interval of time if these events occur with a known average rate = `mean`. 
  | Poisson of mean:float // mean a.k.a. lambda [0, maxint] -> number of events [0 to maxint]
  /// A family of distributions of positive values. The parameters alpha and beta are sometimes called shape and rate.
  | Gamma of float*float // alpha (>0), beta (>0) -> continuous (0 to infinity)
  /// Time between events in a process in which events occur continuously and independently at a constant average `rate = 1/mean`. 
  | Exponential of mean:float // rate lambda (>0) -> continuous [0 to infinity)
  /// A weighted mixture of distributions.
  | Mixture of (float*Distribution) list

  /// Make a distribution which density matches a piecewise linear curve.
  /// Abscissas must be in increasing order.
  /// Oridnates must be positive values. The function scales the density so that its norm = 1.
  static member fromPiecewise (density: (float * float) seq) =
    let x, y = density |> Array.ofSeq |> Array.unzip
    let n = Array.length x
    if seq {for i in 0..n-2 -> x.[i] >= x.[i+1]} |> Seq.reduce (||) then invalidOp "Abscissas must be in increasing order."
    if Array.exists (fun v -> v < 0.) y then invalidOp "Oridnates must be positive values."
    let cc = [for i in 0..n-2 -> 0.5*(y.[i+1]+y.[i])*(x.[i+1]-x.[i])]
    let norm = List.sum cc
    Mixture (cc |> List.mapi (fun i c -> c/norm, Linear(x.[i], x.[i+1], y.[i]/c)))

/// The smallest positive normalized `float` value
let improbable = 2.2250738585072014E-308 // 2^(-1022)
/// Logarithm of `improbable`
let log_improbable = log(improbable) // -708

/// `1.0 - tolerance < 1.0 && 1.0 - 0.5*tolerance = 1.0`
let tolerance = 1.1102230246251565E-16 // 2^(-53)
/// Logarithm of `tolerance`
let log_tolerance = log(tolerance) // -36.7

/// Maximum exact integer `maxint+1.0 = maxint && maxint-1.0 < maxint` 
let maxint = 1.0/tolerance; // 9e15 -- 6 orders of magnitude alrger than int.maxvalue

/// π
let pi = 3.14159265358979323846264338327950288

/// 2π
let pi2 = 6.283185307179586476925286

/// natural logarithm base
let e = 2.71828182845904523536028747135266250

/// sqrt 2π
let sqrt2pi = sqrt(pi2)

/// 1/2 * log 2π
let log2pi = 0.5*log(pi2)

let private isNan = System.Double.IsNaN
let private isInf = System.Double.IsInfinity

/// Sigmoidal function that maps [-infinity,infinity] interval onto [0,1]
let logistic x =
    if x > 1.-log_tolerance then 1. else
    let ex = exp x
    ex / (1. + ex)

/// Inverse logistic transform
let logit p =
    if p > 1. || p < 0. then nan
    elif p = 1. then infinity
    elif p = 0. then -infinity
    else log(p/(1.-p)) 

type summaryType = 
    {count:int; min:float; max:float; mean:float; variance:float}
    override me.ToString() = sprintf "%A" me

/// Produces cumulant summary of the data using fast one-pass algorithm.
let summary data =
    let folder summary d =
        if isNan(d) || isInf(d) then 
            summary 
        else
            let delta = d - summary.mean
            let n = summary.count + 1
            let mean = summary.mean + delta/float n
            {
                count = n
                min = (min d summary.min)
                max = (max d summary.max)
                mean = mean
                variance = summary.variance + delta*(d-mean)
            }
    let pass =
        Seq.fold folder {
                            count=0
                            min=System.Double.PositiveInfinity
                            max=System.Double.NegativeInfinity
                            mean=0.0
                            variance=0.0
                            } data
    if pass.count<2 then
        pass
    else
        let pass = {pass with variance=pass.variance/(float(pass.count-1))}
        pass

type qsummaryType = 
    {min:float; lb95:float; lb68:float; median:float; ub68:float; ub95:float; max:float}
    override me.ToString() = sprintf "%A" me

/// Produces quantile summary of the data.
let qsummary data =
    let a = data |> Seq.filter(fun d -> not (System.Double.IsNaN(d) || System.Double.IsInfinity(d))) |> Seq.toArray
    Array.sortInPlace a
    let n = a.Length
    if n<1 then {min=nan; lb95=nan; lb68=nan; median=nan; ub68=nan; ub95=nan; max=nan}
    else
        let q p =
            // Definition 8 from Hyndman, R. J. and Fan, Y. (1996) Sample quantiles in statistical packages, American Statistician 50, 361–365.
            // This is the same as stats.quantile(...,type=8) from R
            let h = p*(float n + 1./3.)-2./3.
            if h <= 0.0 then a.[0]
            elif h >= float (n-1) then a.[n-1]
            else
                let fh = floor h
                a.[int fh]*(1.0-h+fh) + a.[int fh + 1]*(h - fh)
        {min=a.[0]; lb95=q(0.025); lb68=q(0.16); median=q(0.5); ub68=q(0.84); ub95=q(0.975); max=a.[n-1]}

// adopted from Numerical Recipes: The Art of Scientific Computing, Third Edition (2007), p.257
let private log_gamma x =
    let cof=[|57.1562356658629235; -59.5979603554754912; 14.1360979747417471; -0.491913816097620199; 0.339946499848118887e-4; 0.465236289270485756e-4; -0.983744753048795646e-4; 0.158088703224912494e-3; -0.210264441724104883e-3; 0.217439618115212643e-3; -0.164318106536763890e-3; 0.844182239838527433e-4; -0.261908384015814087e-4; 0.368991826595316234e-5|]
    if x<=0.0 then nan else
        let t = x + 5.24218750000000000 // Rational 671/128.
        let t = (x+0.5)*log(t)-t
        let ser,_ = cof |> Seq.fold (fun (ser,x) c -> let y=x+1.0 in ser+c/y,y) (0.999999999999997092,x)
        t+log(2.5066282746310005*ser/x)

// adopted from "Fast and Accurate Computation of Binomial Probabilities", C. Loader, 2000
let private sfe =
    Seq.unfold (fun (n, lognf) -> 
        if n=0 then 
            Some(0.0, (1,0.0))
        elif n<16 then
            let logn = log(float n)
            Some(lognf+float(n)-log2pi-(float(n)-0.5)*logn, (n+1, lognf+logn)) 
        else None) (0, 0.0) |> Array.ofSeq

let private stirlerr n =
    if (n<16) then sfe.[n]
    else
        let S0 = 1.0/12.0
        let S1 = 1.0/360.0
        let S2 = 1.0/1260.0
        let S3 = 1.0/1680.0
        let S4 = 1.0/1188.0
        let n1 = 1.0/float(n)
        let n2 = n1*n1
        if (n>500) then ((S0-S1*n2)*n1)
        elif (n>80) then ((S0-(S1-S2*n2)*n2)*n1)
        elif (n>35) then ((S0-(S1-(S2-S3*n2)*n2)*n2)*n1)
        else ((S0-(S1-(S2-(S3-S4*n2)*n2)*n2)*n2)*n1)

let private bd0 (x: float, np: float) =
    if (abs(x-np) < 0.1*(x+np)) then
        let v = (x-np)/(x+np)
        let rec next j ej s =
            let s1 = s + ej/float(2*j+1)
            if s1=s then s1
            else next (j+1) (ej*v*v) s1
        next 1 (2.0*x*v*v*v) ((x-np)*v)
    else x*log(x/np)+np-x

let private dbinom (x: int, n: int, p: float) =
//    assert((p>=0.0) && (p<=1.0))
//    assert(n>=0)
//    assert((x>=0) && (x<=n))
    if (p=0.0) then if x=0 then 1.0 else 0.0
    elif (p=1.0) then if x=n then 1.0 else 0.0
    elif (x=0) then exp(float n*log(1.0-p))
    elif (x=n) then exp(float n*log(p))
    else
        let lc = stirlerr(n) - stirlerr(x) - stirlerr(n-x) - bd0(float x, float(n)*p) - bd0(float(n-x), float(n)*(1.0-p))
        exp(lc)*sqrt(float(n)/(pi2*float(x)*float(n-x)));

let private dpois(x: int, lb: float) =
    if (lb=0.0) then if x=0 then 1.0 else 0.0
    elif (x=0) then exp(-lb)
    else exp(-stirlerr(x)-bd0(float x,lb))/sqrt(pi2*float(x));

/// Logarithm of a Probability Distribution Function
let rec log_pdf d v =
    if System.Double.IsNaN(v) then log_improbable
    else
        let result =
            match d with
            | Normal(mean,stdev) -> let dev = (mean-v)/stdev in -0.5*dev*dev - log(sqrt2pi*stdev)
            | LogNormal(mean,stdev) -> let dev = (log mean-log v)/stdev in -0.5*dev*dev - log(sqrt2pi*stdev*v)
            | Uniform(lb,ub) ->
                if (v<lb || v>ub || lb>=ub) then log_improbable
                else -log(ub-lb)
            | LogUniform(lb,ub) ->
                if (v<lb || v>ub || lb>=ub) then log_improbable
                else -log(log ub - log lb) - log v
            | Linear(x1,x2,density) ->
                if v < min x1 x2 || v > max x1 x2 then log_improbable
                elif x1=x2 then infinity else
                let h = 2./abs(x2-x1)
                let p1 = if density<improbable then improbable elif density > h then h else density // p1 = 2*a*x1+b
                let p2 = h-p1 // 0.5*(p1+p2)*abs(x2-x1) == 1; p2 = 2*a*x2+b
                log(p1+(v-x1)*(p2-p1)/(x2-x1))
            | Exponential(mean) -> 
                if mean<=0.0 || mean = infinity then log_improbable else
                    -log(mean) - v/mean
            | Gamma(a,b) ->
                a*log(b) - log_gamma(a) + (a-1.0)*log(v) - b*v
            | Bernoulli(fraction) -> 
                if (fraction<tolerance || fraction>1.0-tolerance) then log_improbable
                elif v>0.5 then log(fraction) 
                else log(1.0-fraction)
            | Binomial(n, p) ->
                // log(dbinom(int v, int n, p))
                if (p<0.0) || (p>1.0) || (n<0) || (v<0.0) || (v>float n) then log_improbable
                else log(dbinom(int v, int n, p))
            | NegativeBinomial(mean, r) ->
                if mean<=0.0 || r<=0.0 || v<0.0 || v>maxint then log_improbable else
                    let k = round v
                    r*log(r/(mean+r))+k*log(mean/(mean+r))+log_gamma(r+k)-log_gamma(k+1.0)-log_gamma(r)
            | Poisson(lambda) ->
                if (lambda<0.0 || lambda>float System.Int32.MaxValue) then log_improbable
                // log(dpois(int v, lambda))
                elif (lambda=0.0) then if v=0.0 then 0.0 else log_improbable
                elif (v=0.0) then -lambda
                else let x = int v in - stirlerr(x) - bd0(float x,lambda) - 0.5*log(pi2*float(x));
            | Mixture(components) -> log(components |> List.fold (fun s (w,d) -> s+w*exp(log_pdf d v)) 0.0)
            //| _ -> raise (System.NotImplementedException())
        if System.Double.IsNaN(result) || System.Double.IsInfinity(result) then 
            log_improbable
        else
            result

// http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/mt19937ar.c
//
//   Adopted from a C-program for MT19937, with initialization improved 2002/1/26.
//   Coded by Takuji Nishimura and Makoto Matsumoto.
//
//   Copyright (C) 1997 - 2002, Makoto Matsumoto and Takuji Nishimura,
//   All rights reserved.                          
//
//   Redistribution and use in source and binary forms, with or without
//   modification, are permitted provided that the following conditions
//   are met:
//
//     1. Redistributions of source code must retain the above copyright
//        notice, this list of conditions and the following disclaimer.
//
//     2. Redistributions in binary form must reproduce the above copyright
//        notice, this list of conditions and the following disclaimer in the
//        documentation and/or other materials provided with the distribution.
//
//     3. The names of its contributors may not be used to endorse or promote 
//        products derived from this software without specific prior written 
//        permission.
//
//   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
//   "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
//   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
//   A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT OWNER OR
//   CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
//   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
//   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
//   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
//   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
//
//   Any feedback is very welcome.
//   http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/emt.html
//   email: m-mat @ math.sci.hiroshima-u.ac.jp (remove space)

type MT19937 private (
                        mt:uint32[], // the array for the state vector
                        idx:int      // index of the next word from the state (0..N)
                        ) =
    // Period parameters
    [<Literal>] static let N = 624
    [<Literal>] static let M = 397
    [<Literal>] static let MATRIX_A = 0x9908b0dfu   // constant vector a
    [<Literal>] static let UPPER_MASK = 0x80000000u // most significant w-r bits
    [<Literal>] static let LOWER_MASK = 0x7fffffffu // least significant r bits

    let mutable mti = idx // mti==N+1 means mt[N] is not initialized

    // initializes mt[N] with a seed
    static let init_genrand s =
        let mt:uint32[] = Array.zeroCreate N // the array for the state vector
        mt.[0] <- s &&& 0xffffffffu
        for mti = 1 to N-1 do
            mt.[mti] <- 
                (1812433253u * (mt.[mti-1] ^^^ (mt.[mti-1] >>> 30)) + uint32 mti)
                // See Knuth TAOCP Vol2. 3rd Ed. P.106 for multiplier. 
                // In the previous versions, MSBs of the seed affect 
                // only MSBs of the array mt[].
                // 2002/01/09 modified by Makoto Matsumoto
            mt.[mti] <- mt.[mti] &&& 0xffffffffu
            // for >32 bit machines
        mt

    static let init_by_array (init_key:uint32[]) =
        let mt = init_genrand(19650218u)
        let mutable i = 1
        let mutable j = 0
        let key_length = Array.length init_key
        for k = max key_length N downto 1 do
            mt.[i] <- (mt.[i] ^^^ ((mt.[i-1] ^^^ (mt.[i-1] >>> 30)) * 1664525u)) + init_key.[j] + uint32 j // non linear
            mt.[i] <- mt.[i] &&& 0xffffffffu // for WORDSIZE > 32 machines
            i <- i + 1
            j <- j + 1
            if i >= N then 
                mt.[0] <- mt.[N-1]
                i <- 1
            if j >= key_length then j <- 0
        for k = N-1 downto 1 do
            mt.[i] <- (mt.[i] ^^^ ((mt.[i-1] ^^^ (mt.[i-1] >>> 30)) * 1566083941u)) - uint32 i; // non linear
            mt.[i] <- mt.[i] &&& 0xffffffffu // for WORDSIZE > 32 machines
            i <- i + 1
            if i >= N then 
                mt.[0] <- mt.[N-1]
                i <- 1

        mt.[0] <- 0x80000000u //* MSB is 1; assuring non-zero initial array */ 
        mt

    // generates a random number on [0,0xffffffff]-interval 
    let genrand_int32() : uint32 =
        let mutable y = 0u
        let mag01 = [|0x0u; MATRIX_A|]
        // mag01[x] = x * MATRIX_A  for x=0,1

        if (mti >= N) then // generate N words at one time
            for  kk=0 to N-M-1 do
                y <- (mt.[kk] &&& UPPER_MASK) ||| (mt.[kk+1] &&& LOWER_MASK)
                mt.[kk] <- mt.[kk+M] ^^^ (y >>> 1) ^^^ mag01.[int(y &&& 0x1u)]
            for kk = N-M to N-2 do
                y <- (mt.[kk] &&& UPPER_MASK) ||| (mt.[kk+1] &&& LOWER_MASK)
                mt.[kk] <- mt.[kk+(M-N)] ^^^ (y >>> 1) ^^^ mag01.[int(y &&& 0x1u)]
            y <- (mt.[N-1] &&& UPPER_MASK) ||| (mt.[0] &&& LOWER_MASK)
            mt.[N-1] <- mt.[M-1] ^^^ (y >>> 1) ^^^ mag01.[int(y &&& 0x1u)];

            mti <- 0
  
        y <- mt.[mti]
        mti <- mti + 1

        // Tempering
        y <- y ^^^ (y >>> 11)
        y <- y ^^^ ((y <<< 7) &&& 0x9d2c5680u)
        y <- y ^^^ ((y <<< 15) &&& 0xefc60000u)
        y <- y ^^^ (y >>> 18)

        y

    // generates a random number on [0,1)-real-interval
    let genrand_float() =
        float(genrand_int32())*(1.0/4294967296.0) 
        // divided by 2^32

    // tables for ziggurat algorithm http://www.boost.org/doc/libs/1_60_0/boost/random/normal_distribution.hpp
    static let table_x = [|
        3.7130862467403632609; 3.4426198558966521214; 3.2230849845786185446; 3.0832288582142137009;
        2.9786962526450169606; 2.8943440070186706210; 2.8231253505459664379; 2.7611693723841538514;
        2.7061135731187223371; 2.6564064112581924999; 2.6109722484286132035; 2.5690336259216391328;
        2.5300096723854666170; 2.4934545220919507609; 2.4590181774083500943; 2.4264206455302115930;
        2.3954342780074673425; 2.3658713701139875435; 2.3375752413355307354; 2.3104136836950021558;
        2.2842740596736568056; 2.2590595738653295251; 2.2346863955870569803; 2.2110814088747278106;
        2.1881804320720206093; 2.1659267937448407377; 2.1442701823562613518; 2.1231657086697899595;
        2.1025731351849988838; 2.0824562379877246441; 2.0627822745039633575; 2.0435215366506694976;
        2.0246469733729338782; 2.0061338699589668403; 1.9879595741230607243; 1.9701032608497132242;
        1.9525457295488889058; 1.9352692282919002011; 1.9182573008597320303; 1.9014946531003176140;
        1.8849670357028692380; 1.8686611409895420085; 1.8525645117230870617; 1.8366654602533840447;
        1.8209529965910050740; 1.8054167642140487420; 1.7900469825946189862; 1.7748343955807692457;
        1.7597702248942318749; 1.7448461281083765085; 1.7300541605582435350; 1.7153867407081165482;
        1.7008366185643009437; 1.6863968467734863258; 1.6720607540918522072; 1.6578219209482075462;
        1.6436741568569826489; 1.6296114794646783962; 1.6156280950371329644; 1.6017183802152770587;
        1.5878768648844007019; 1.5740982160167497219; 1.5603772223598406870; 1.5467087798535034608;
        1.5330878776675560787; 1.5195095847593707806; 1.5059690368565502602; 1.4924614237746154081;
        1.4789819769830978546; 1.4655259573357946276; 1.4520886428822164926; 1.4386653166774613138;
        1.4252512545068615734; 1.4118417124397602509; 1.3984319141236063517; 1.3850170377251486449;
        1.3715922024197322698; 1.3581524543224228739; 1.3446927517457130432; 1.3312079496576765017;
        1.3176927832013429910; 1.3041418501204215390; 1.2905495919178731508; 1.2769102735516997175;
        1.2632179614460282310; 1.2494664995643337480; 1.2356494832544811749; 1.2217602305309625678;
        1.2077917504067576028; 1.1937367078237721994; 1.1795873846544607035; 1.1653356361550469083;
        1.1509728421389760651; 1.1364898520030755352; 1.1218769225722540661; 1.1071236475235353980;
        1.0922188768965537614; 1.0771506248819376573; 1.0619059636836193998; 1.0464709007525802629;
        1.0308302360564555907; 1.0149673952392994716; 0.99886423348064351303; 0.98250080350276038481;
        0.96585507938813059489; 0.94890262549791195381; 0.93161619660135381056; 0.91396525100880177644;
        0.89591535256623852894; 0.87742742909771569142; 0.85845684317805086354; 0.83895221428120745572;
        0.81885390668331772331; 0.79809206062627480454; 0.77658398787614838598; 0.75423066443451007146;
        0.73091191062188128150; 0.70647961131360803456; 0.68074791864590421664; 0.65347863871504238702;
        0.62435859730908822111; 0.59296294244197797913; 0.55869217837551797140; 0.52065603872514491759;
        0.47743783725378787681; 0.42654798630330512490; 0.36287143102841830424; 0.27232086470466385065;
        0.
    |]

    static let table_y = [|
        0.; 0.0026696290839025035092; 0.0055489952208164705392; 0.0086244844129304709682;
        0.011839478657982313715; 0.015167298010672042468; 0.018592102737165812650; 0.022103304616111592615;
        0.025693291936149616572; 0.029356317440253829618; 0.033087886146505155566; 0.036884388786968774128;
        0.040742868074790604632; 0.044660862200872429800; 0.048636295860284051878; 0.052667401903503169793;
        0.056752663481538584188; 0.060890770348566375972; 0.065080585213631873753; 0.069321117394180252601;
        0.073611501884754893389; 0.077950982514654714188; 0.082338898242957408243; 0.086774671895542968998;
        0.091257800827634710201; 0.09578784912257815216; 0.10036444102954554013; 0.10498725541035453978;
        0.10965602101581776100; 0.11437051244988827452; 0.11913054670871858767; 0.12393598020398174246;
        0.12878670619710396109; 0.13368265258464764118; 0.13862377998585103702; 0.14361008009193299469;
        0.14864157424369696566; 0.15371831220958657066; 0.15884037114093507813; 0.16400785468492774791;
        0.16922089223892475176; 0.17447963833240232295; 0.17978427212496211424; 0.18513499701071343216;
        0.19053204032091372112; 0.19597565311811041399; 0.20146611007620324118; 0.20700370944187380064;
        0.21258877307373610060; 0.21822164655637059599; 0.22390269938713388747; 0.22963232523430270355;
        0.23541094226572765600; 0.24123899354775131610; 0.24711694751469673582; 0.25304529850976585934;
        0.25902456739871074263; 0.26505530225816194029; 0.27113807914102527343; 0.27727350292189771153;
        0.28346220822601251779; 0.28970486044581049771; 0.29600215684985583659; 0.30235482778947976274;
        0.30876363800925192282; 0.31522938806815752222; 0.32175291587920862031; 0.32833509837615239609;
        0.33497685331697116147; 0.34167914123501368412; 0.34844296754987246935; 0.35526938485154714435;
        0.36215949537303321162; 0.36911445366827513952; 0.37613546951445442947; 0.38322381105988364587;
        0.39038080824138948916; 0.39760785649804255208; 0.40490642081148835099; 0.41227804010702462062;
        0.41972433205403823467; 0.42724699830956239880; 0.43484783025466189638; 0.44252871528024661483;
        0.45029164368692696086; 0.45813871627287196483; 0.46607215269457097924; 0.47409430069824960453;
        0.48220764633483869062; 0.49041482528932163741; 0.49871863547658432422; 0.50712205108130458951;
        0.51562823824987205196; 0.52424057267899279809; 0.53296265938998758838; 0.54179835503172412311;
        0.55075179312105527738; 0.55982741271069481791; 0.56902999107472161225; 0.57836468112670231279;
        0.58783705444182052571; 0.59745315095181228217; 0.60721953663260488551; 0.61714337082656248870;
        0.62723248525781456578; 0.63749547734314487428; 0.64794182111855080873; 0.65858200005865368016;
        0.66942766735770616891; 0.68049184100641433355; 0.69178914344603585279; 0.70333609902581741633;
        0.71515150742047704368; 0.72725691835450587793; 0.73967724368333814856; 0.75244155918570380145;
        0.76558417390923599480; 0.77914608594170316563; 0.79317701178385921053; 0.80773829469612111340;
        0.82290721139526200050; 0.83878360531064722379; 0.85550060788506428418; 0.87324304892685358879;
        0.89228165080230272301; 0.91304364799203805999; 0.93628268170837107547; 0.96359969315576759960;
        1.
    |]

    /// generates a sample from standard normal distribution N(0,1) using ziggurat algorithm.
    let znorm() =
        let tail() =
            let exponential() = -log(1.0-genrand_float())
            let tail_start = table_x.[1]
            let mutable r = System.Double.PositiveInfinity
            while System.Double.IsPositiveInfinity r do
                let x = exponential() / tail_start
                let y = exponential()
                if 2.0*y > x*x then r <- x+tail_start
            r

        let mutable r = System.Double.PositiveInfinity
        while System.Double.IsPositiveInfinity r do
            let digit = int(genrand_int32() &&& 255u)
            let sign = if digit &&& 1 = 0 then -1.0 else 1.0 // float(int(digit &&& 1)*2-1)
            let i = digit >>> 1
            let x = genrand_float()*table_x.[i]
            if x<table_x.[i+1] then r <- x*sign
            elif i=0 then r <- tail()*sign
            else
                let y = table_y.[i] + genrand_float()*(table_y.[i+1]-table_y.[i])
                if y < exp(-0.5*x*x) then r <- x*sign 
        r


#if BOX_MULLER
    // Box-Muller is generally slower and requires additional state

    let mutable rnorm_phase = false
    let mutable rnorm_2 = 0.0
    let mutable rnorm_f = 0.0

    /// generates a sample from standard normal distribution N(0,1) using Box-Muller method.
    let rnorm () =
        if rnorm_phase then
            rnorm_phase <- false
            rnorm_2*rnorm_f
        else
            rnorm_phase <- true
            let mutable rnorm_1 = 0.0
            let mutable s = 1.0
            while (s>=1.0) do
                rnorm_1 <- genrand_float()*2.0-1.0
                rnorm_2 <- genrand_float()*2.0-1.0
                s <- rnorm_1*rnorm_1 + rnorm_2*rnorm_2
            rnorm_f <- sqrt(-2.0*log(s)/s)
            rnorm_1*rnorm_f
#endif

    do
        if mt.Length <> N then failwith (sprintf "State must be an array of length %d" N)

    new () =
        let state = init_genrand (5489u)
        MT19937(state, N)
    
    new (seed:uint32) =
        let state = init_genrand seed
        MT19937(state, N)

    new (?seed:uint32) =
        let state = init_genrand (defaultArg seed 5489u)
        MT19937(state, N)

    new (seed:uint32[]) =
        if Array.length seed = N+1 && seed.[N] < 2u + uint32 N then
            let state = Array.init N (fun i -> seed.[i])
            let idx = int (seed.[N])
            MT19937(state, idx)
        else
            let state = init_by_array(seed)
            MT19937(state, N)
    member private x.getMt = Array.copy mt
    member private x.getIdx = mti
    new(copy:MT19937) =
        MT19937(copy.getMt, copy.getIdx)

    /// returns an array that allows to exactly restore the state of the generator.
    member x.get_seed() = [| yield! mt; yield uint32 mti|]

    /// generates a random number on [0,0xffffffff]-interval 
    member __.uniform_uint32() = genrand_int32()

    /// generates a random number on [0,1)-real-interval
    member __.uniform_float64() = genrand_float()

    /// generates a random number on [0,max]-int-interval
    member __.uniform_int (max:int) =
        if max < 0 then invalidArg "max" "The value cannot be negative"
        elif max = 0 then 0
        // if typeof<max> were uint32:
        //elif max = System.UInt32.MaxValue then x.genrand_int32()
        else
            let umax = uint32 max
            let bucket_size = // (System.UInt32.MaxValue+1)/(max+1)
                let bs = System.UInt32.MaxValue / (umax + 1u)
                if System.UInt32.MaxValue % (umax + 1u) = umax then bs + 1u else bs
            // rejection algorithm
            let mutable r = genrand_int32() / bucket_size
            while r > umax do r <- genrand_int32() / bucket_size
            int r

    /// generates 'true' with probability 'p' or 'false' with probability '1-p'
    member __.bernoulli(p) =
        if p <= 0.0 then false
        elif p >= 1.0 then true
        else float(genrand_int32()) <= p*float(System.UInt32.MaxValue)

    /// generates a sample from standard normal distribution N(0,1) using ziggurat algorithm.
    member __.normal() = znorm()

#if BOX_MULLER
    /// generates a sample from standard normal distribution N(0,1) using Box-Muller algorithm.
    member __.normal_bm() = rnorm()
#endif


let rec draw (gen:MT19937) d = // random number generator
    let rng_norm(mean, stdev) = mean + stdev * gen.normal()
    let rng_unif(lower, upper) = lower + gen.uniform_float64()*(upper-lower)
    let rng_poisson lambda = 
        if lambda>30.0 then
                rng_norm(lambda,sqrt(lambda))
            else
                let ell = exp(-lambda)
                let rec step p k = if p<ell then k-1 else step (p*gen.uniform_float64()) (k+1)
                float(step 1.0 0)
    let rec rand_positive () = let u = gen.uniform_float64() in if u>0.0 then u else rand_positive()
    let rng_exp mean = -log(rand_positive()) * mean
    let rng_gamma a b =
        let p = e/(a+e)
        let s = sqrt(2.0*a-1.0)
        let rec rand_positive () = let u = gen.uniform_float64() in if u>0.0 then u else rand_positive()
        if a<1.0 then
            // small values of alpha
            // from Knuth
            let rec iter() =
                // generate and reject
                let u = gen.uniform_float64()
                let v = rand_positive()
                let x,q = if u < p then let x=exp(log(v)/a) in x,exp(-x) else let x = 1.0 - log v in x, exp((a-1.0)*log x)
                if gen.uniform_float64()<q then
                    x/b
                else
                    iter()
            iter()
        elif a=1.0 then
            rng_exp(1.0)/b
        elif a<20.0 && floor a = a then
            // _Alpha is small integer, compute directly:
            // -∑_(k=1)^n▒ln⁡〖U_k 〗 ~Γ(n,1)
            // Where U  is uniformly distributed on (0, 1] (https://en.wikipedia.org/wiki/Gamma_distribution)
            let product = seq { for _ in 1..int a -> rand_positive()} |> Seq.reduce (*)
            -log(product)/b
        else
            // no shortcuts
            let rec iter () =
                let y = tan(pi*gen.uniform_float64())
                let x = s*y+a-1.0
                if 0.0 < x && gen.uniform_float64() <= (1.0+y*y)*exp((a-1.0)*log(x/(a-1.0))-s*y) then
                    x/b
                else iter()
            iter()
    match d with
    | Normal(mean,stdev) -> rng_norm(mean,stdev)
    | LogNormal(mean,stdev) -> exp(rng_norm(log mean,stdev))
    | Gamma(a,b) -> rng_gamma a b
    | Exponential(mean) -> rng_exp mean
    | Uniform(lower, upper) -> rng_unif(lower, upper)
    | LogUniform(lower, upper) -> exp(rng_unif(log lower, log upper))
    | Linear(x1,x2,density) ->
        if x1=x2 then x1 else
        let h = 2./(x2-x1)
        let p, pmin, pmax = if h>improbable then density, improbable, h else -density, h, -improbable
        let p1 = if p<pmin then pmin elif p>pmax then pmax else p // p1 = 2*a*x1+b
        let p2 = h-p1 // 0.5*(p1+p2)*(x2-x1) == 1; p2 = 2*a*x2+b
        let a4 = (p2-p1)*h // 4*a
        let b = p1 - 0.5*a4*x1
        let y = gen.uniform_float64()
        let absd = sqrt(p1*p1 + a4*y)
        let d = if h>0. then absd else -absd
        (d - b) * 2. / a4
    | Bernoulli(fraction) -> if gen.uniform_float64()<fraction then 1.0 else 0.0
    | Poisson(lambda) -> rng_poisson lambda
    | Binomial(n,p) ->
        let rec step n k = if n<=0 then k else step (n-1) (if gen.uniform_float64()<p then k+1 else k)
        float(step (int n) 0 )
    | NegativeBinomial(mean,r) ->
        // adopted from VC++2012u3 <random>
        let v = rng_gamma r (r/mean)
        rng_poisson(v)
    | Mixture(components) ->
        let rec oneof f c =
            match c with 
            | [] -> failwith "empty mixture!" 
            | (w,d)::tail ->
                if f <= w then draw gen d else oneof (f-w) tail
        oneof (gen.uniform_float64()) components
    //| _ -> raise (System.NotImplementedException())


// Computes Pearson's correlation coefficient for two float arrays
// The Pearson correlation is defined only if both of the standard deviations are finite and both of them are nonzero.
// Returns NaN, otherwise.
let correlation (x:float[]) (y:float[]) =
    if x.Length <> y.Length then invalidOp "Different lengths of arrays"            
    let filtered = Seq.zip x y |> Seq.filter (fun(u,v) -> not (isNan(u) || isNan(v) || isInf(u) || isInf(v))) |> Array.ofSeq;
    let n = filtered.Length 
    if n <= 1 then System.Double.NaN else
    let _x, _y = Array.map fst filtered, Array.map snd filtered
    let sx, sy = summary _x, summary _y
    let stdx, stdy = sqrt sx.variance, sqrt sy.variance
    if stdx = 0.0 || stdy = 0.0 || isInf(stdx) || isInf(stdy) then System.Double.NaN else
    let d1 = float(n) * sx.mean * sy.mean
    let d2 = float(n-1) * stdx * stdy
    ((filtered |> Array.map (fun(s,t)->s*t) |> Array.sum) - d1)/d2

// KDE

// adopted from MathNet.Numerics
// https://github.com/mathnet/mathnet-numerics/blob/v3.9.0/src/Numerics/IntegralTransforms/Fourier.RadixN.cs

open System.Numerics

let private InverseScaleByOptions(samples:Complex[]) =
    let scalingFactor = 1.0/(float samples.Length)
    for i in 0..samples.Length-1 do
        samples.[i] <- samples.[i] * Complex(scalingFactor, 0.)

let private ForwardScaleByOptions(samples:Complex[]) =
    let scalingFactor = sqrt(1.0/(float samples.Length))
    for i in 0..samples.Length-1 do
        samples.[i] <- samples.[i] * Complex(scalingFactor, 0.)


let private Radix2Reorder(samples:'T[]) =
            let mutable j = 0
            for i in 0..samples.Length - 2 do
                if (i < j) then
                    let temp = samples.[i]
                    samples.[i] <- samples.[j]
                    samples.[j] <- temp

                let mutable m = samples.Length
                let mutable cont = true
                while cont do
                    m <- m >>> 1;
                    j <- j ^^^ m;
                    cont <- (j &&& m) = 0

let private Radix2Step(samples:Complex[], exponentSign:int, levelSize:int, k:int) =
            // Twiddle Factor
            let exponent = (float exponentSign*float k)*pi/float levelSize
            let w = Complex(cos(exponent), sin(exponent))
            let step = levelSize <<< 1
            for i in k..step..samples.Length-1 do
                let ai = samples.[i]
                let t = w*samples.[i + levelSize]
                samples.[i] <- ai + t
                samples.[i + levelSize] <- ai - t

let private Radix2(samples:Complex[], exponentSign:int) =
            let rec is_power_two x p =
                if x = p then true
                elif x < p then false
                else is_power_two x (2*p)
            if not <| is_power_two samples.Length 1 then invalidArg "samples" "The array length must be a power of 2." 

            Radix2Reorder(samples)
            let mutable levelSize = 1 
            while levelSize < samples.Length do
                for k = 0 to levelSize-1 do Radix2Step(samples, exponentSign, levelSize, k)
                levelSize <- levelSize * 2



let private fi x = float(x)

/// Inverse Fast Fourier Transform.
let ifft (xs:Complex[]) = 
    let samples = Array.copy xs
    Radix2(samples,1)
    let scalingFactor = 1.0/(float samples.Length)
    for i in 0..samples.Length-1 do
        let v = samples.[i]
        samples.[i] <- Complex(scalingFactor * v.Real, scalingFactor * v.Imaginary)
    samples

/// Fast Fourier transform. 
let fft (xs:Complex[]) = 
    let samples = Array.copy xs
    Radix2(samples,-1)
    samples

let private (|Even|Odd|) input = if input % 2 = 0 then Even else Odd

/// Descrete cosine transform.
let dct (rxs:float[]) = 
    let xs = Array.map (fun x -> Complex(x,0.0)) rxs
    let len = xs.Length
    let n = fi len
    let weights = 
        let myseq = Seq.init (len-1) (fun x -> Complex(2.0, 0.0) * Complex.Exp(Complex(0.0, fi (x+1) * pi / (2.0*n))))
        seq { yield Complex(2.0, 0.0); yield! myseq } |> Array.ofSeq
    let backpermute (arr:Complex[]) ind = ind |> Seq.map (fun i -> arr.[i])
    let interleaved = 
        let en = Seq.init ((len+1)/2) (fun i -> i * 2)
        let en2 = Seq.init (len/2) (fun i -> len - (i*2) - 1)
        backpermute xs (seq{ yield! en; yield! en2 }) |> Array.ofSeq

    Array.map2 (fun (a:Complex) (b:Complex) -> (a*b).Real) weights (fft interleaved)


/// Inverse discrete cosine transform.
// idct :: U.Vector CD -> U.Vector Double
// http://hackage.haskell.org/package/statistics-0.10.0.0/docs/src/Statistics-Transform.html
let idct (rxs:float[]) = 
    let xs = Seq.map (fun x -> Complex(x,0.0)) rxs
    let len = rxs.Length
    let weights = 
        let n = fi len
        let coeff k = Complex(2.0 * n, 0.0) * Complex.Exp(Complex(0.0, fi (k+1) * pi / (2.0*n)))
        seq { yield Complex(n,0.0); for i in 0..len-2 do yield coeff i }
    let vals = (Array.map (fun (c:Complex) -> c.Real) << ifft) (Seq.map2 (*) weights xs |> Array.ofSeq)
    let interleave z =
        let hz = z >>> 1
        match z with
            | Even _ -> vals.[hz]
            | Odd _ -> vals.[len - hz - 1]
    [| for i in 0..len-1 do yield interleave(i) |]

// http://hackage.haskell.org/package/statistics-0.10.5.0/docs/src/Statistics-Function.html#nextHighestPowerOfTwo
//
// Efficiently compute the next highest power of two for a
// non-negative integer.  If the given value is already a power of
// two, it is returned unchanged.  If negative, zero is returned.
let private nextHighestPowerOfTwo n =
  let   i0   = n - 1
  let   i1   = i0  ||| (i0 >>> 1)
  let   i2   = i1  ||| (i1 >>> 2)
  let   i4   = i2  ||| (i2 >>> 4)
  let   i8   = i4  ||| (i4 >>> 8)
  let   i16  = i8  ||| (i8 >>> 16)
  let _i32 = i16 ||| (i16 >>> 32)
  1 + _i32

let histogram_ n xmin xmax xs =
    if n < 1 then invalidArg "n" "must be > 0"
    let isNotFinite x = System.Double.IsNaN x || System.Double.IsInfinity x
    if  isNotFinite xmin then invalidArg "xmin" (sprintf "is %g" xmin)
    if isNotFinite xmax then invalidArg "xmax" (sprintf "is %g" xmax)
    if xmin >= xmax then invalidOp "xmin should be less than xmax"
    let h = Array.zeroCreate n
    let step = (xmax - xmin) / float n
    let add x =
        if not(isNan x) && x >= xmin && x <= xmax then
            let idx = min (n-1) (int((x-xmin)/step))
            h.[idx] <- h.[idx] + 1
    xs |> Seq.iter add
    h

/// Approximate comparison of two double values.
/// Tolerance `ulps` is in units of least precision.
let within (ulps:uint32) a b =
    // See e.g. "Comparing Floating Point Numbers, 2012 Edition" by  Bruce Dawson
    // https://randomascii.wordpress.com/2012/02/25/comparing-floating-point-numbers-2012-edition/
    let ai = System.BitConverter.DoubleToInt64Bits a
    let bi = System.BitConverter.DoubleToInt64Bits b
    let cmp ai bi = if ai<=bi then bi-ai <= int64 ulps else ai-bi <= int64 ulps
    if ai<0L && bi>=0L then cmp (System.Int64.MinValue-ai) bi
    elif ai>=0L && bi<0L then cmp ai (System.Int64.MinValue-bi)
    else cmp ai bi 

/// Root of a function using Ridders method.
//   Ridders, C.F.J. (1979) A new algorithm for computing a single
//   root of a real continuous function.
//   /IEEE Transactions on Circuits and Systems/ 26:979--980.
let ridders tolerance (lb, ub) (f : float->float) =
    // The function must have opposite signs when evaluated at the lower 
    // and upper bounds of the search (i.e. the root must be bracketed). 

    let rec iter a fa b fb i =
        if 100 <= i then None // Too many iterations performed. Fail
        else
        if within 1u a b then Some a // Root is bracketed within 1 ulp. No improvement could be made
        else
        let d = abs(b-a)
        let dm = (b-a) * 0.5
        let m = a + dm
        let fm = f m
        if 0.0 = fm then Some m else
        let dn = float(sign(fb - fa)) * dm * fm / sqrt(fm*fm - fa*fb)
        let n = m - float(sign dn) * min (abs dn) (abs dm - 0.5 * tolerance)
        if d < tolerance then Some n else
        if n=a || n=b then
            // Ridder's approximation coincide with one of old bounds. Revert to bisection 
            if 0 > sign fm * sign fa then iter a fa m fm (i+1)
            else iter m fm b fb (i+1)
        else
        let fn = f n
        if 0.0 = fn then Some n
        elif 0.0 > fn*fm then iter n fn m fm (i+1)
        elif 0.0 > fn*fa then iter a fa n fn (i+1)
        else iter n fn b fb (i+1)
                    
    if not (tolerance>=0.0) then invalidArg "tolerance" "must be greater than 0.0"
    let flb  = f lb
    if 0.0 = flb then Some lb else
    let fub = f ub
    if 0.0 = fub then Some ub
    elif 0.0 < fub*flb then None // root is not bracketed
    else iter lb flb ub fub 0

// from http://hackage.haskell.org/package/statistics-0.10.5.0/docs/src/Statistics-Sample-KernelDensity.html#kde
//
/// Gaussian kernel density estimator for one-dimensional data, using
/// the method of Botev et al.
//
// Botev. Z.I., Grotowski J.F., Kroese D.P. (2010). Kernel density estimation via diffusion. 
// /Annals of Statistics/ 38(5):2916-2957. <http://arxiv.org/pdf/1011.2602>
//
// The result is a pair of vectors, containing:
//
// * The coordinates of each mesh point.
//
// * Density estimates at each mesh point.
//
// n0 The number of mesh points to use in the uniform discretization
// of the interval @(min,max)@.  If this value is not a power of
// two, then it is rounded up to the next power of two.
//
// min Lower bound (@min@) of the mesh range.
// max Upper bound (@max@) of the mesh range.
// NaN in the sample are ignored.
let kde2 n0 min max (sample:float seq) =
    // check kde2 arguments
    if sample = null then invalidArg "sample" "cannot be null"
    else if(n0 = 1) then invalidArg "n0" "cannot be 1"
    else
        let xs = Seq.filter (System.Double.IsNaN >> not) sample |> Array.ofSeq
        if Array.isEmpty xs then invalidArg "sample" "doesn't contain numeric values"
        let m_sqrt_2_pi = sqrt (2.0*pi)
        let m_sqrt_pi = sqrt pi
        let r = max - min
        let len = fi xs.Length
        let ni = nextHighestPowerOfTwo n0    
        let n = fi ni
        let sqr a = a*a
        let mesh = 
            let d = r/(n - 1.0)
            Array.init ni (fun z -> min + d * fi z)

        let density =
            let a = 
                let h = Seq.map (fun x -> float(x) / len) (histogram_ ni min max xs) |> Array.ofSeq
                let sh = Array.sum h
                (dct << Array.map (fun p -> p/sh)) h        
        
            let iv = [| for i in 1..ni-1 do yield sqr(fi i) |]
            let a2v = a |> Seq.skip(1) |> Seq.map (fun q -> sqr(q*0.5)) |> Array.ofSeq
            let t_star =                         
                let rec f q t = 
                    let g i a2 =  i ** q * a2 * exp ((-i) * sqr(pi) * t)                
                    2.0 * pi ** (q*2.0) * Seq.sum (Seq.map2 g iv a2v)
                let rec go s h : float = 
                    let si = fi s
                    let k0 = 
                        let enum = seq{ for i in 1 .. s do yield 2*i - 1 } 
                        fi(Seq.fold (*) 1 enum) / m_sqrt_2_pi
                    let _const = (1.0 + 0.5 ** (si+0.5)) / 3.0
                    let time = (2.0 * _const * k0 / len / h) ** (2.0 / (3.0 + 2.0 * si))
                    if s=1 then h else go (s-1) (f si time) 
            
                let eq x = x - (len * (2.0 * m_sqrt_pi) * go 6 (f 7.0 x)) ** (-0.4)
                match ridders 1e-14 (0.0,0.1) eq with Some root -> root | None -> (0.28 * len ** (-0.4))
                
            let f2 b z = b * exp (sqr z * sqr pi * t_star * (-0.5)) 
            let a2 = Seq.map2 f2 a [| for i in 0..ni-1 do yield fi i |] |> Array.ofSeq
            let a1 = idct a2
            let a0 = Array.map (fun x -> x / (2.0*r)) a1
            a0
        (mesh, density)

/// Gaussian kernel density estimator for one-dimensional data, using
/// the method of Botev et al.
//
// The result is a pair of vectors, containing:
//
// * The coordinates of each mesh point.  The mesh interval is chosen
//   to be 20% larger than the range of the sample.  (To specify the
//   mesh interval, use 'kde2'.)
//
// * Density estimates at each mesh point.
//
// n0 The number of mesh points to use in the uniform discretization
// of the interval @(min,max)@.  If this value is not a power of
// two, then it is rounded up to the next power of two.
let kde n0 (xs:float seq) =
    if(xs = null) then invalidArg "sample" "cannot be empty"
    else
        let mutable max = System.Double.MinValue
        let mutable min = System.Double.MaxValue
        let range =         
            if Seq.isEmpty xs then 
                min <- 0.0
                max <- 0.0
                1.0  // unreasonable guess
            else
                xs |> Seq.iter (fun xsi -> 
                  if max < (xsi) then 
                       max <- xsi
                  if min > (xsi) then 
                       min <- xsi)
                if min >= max then 1.0 else max - min
        kde2 n0 (min - range/10.0) (max + range/10.0) xs
